/**
 * libcurl-lite — Implementation routing HTTP through browser fetch() via JSPI.
 *
 * IPC protocol (mirrors subprocess_shim.py pattern):
 *   Request:  write to /tmp/.curl_request  (line1: "METHOD URL", then headers)
 *             write to /tmp/.curl_request_body (binary body, if any)
 *   Dispatch: system("__dispatch_curl")  — JSPI suspends, JS does fetch()
 *   Response: read /tmp/.curl_response (line1: status code, then headers)
 *             read /tmp/.curl_response_body (binary body)
 */

#include "curl/curl.h"
#include <limits.h>
#include <stdint.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#if !defined(_WIN32)
#include <strings.h>
#endif

/* ------------------------------------------------------------------ */
/*  Internal handle struct                                             */
/* ------------------------------------------------------------------ */

typedef struct
{
    /* Request config */
    char *url;
    char *custom_method;
    char *postfields;
    long postfieldsize;
    char *useragent;
    char *accept_encoding;
    struct curl_slist *headers;
    char *errorbuffer;

    /* Behavior flags */
    long follow_location;
    long max_redirs;
    long timeout;
    long connect_timeout;
    long nobody; /* HEAD request */
    long post;
    long fail_on_error;
    long verbose;

    /* Callbacks */
    curl_write_callback write_cb;
    void *write_data;
    curl_write_callback header_cb;
    void *header_data;

    /* Response state (populated after perform) */
    long response_code;
    char *content_type;
    curl_off_t download_size;
} CurlHandle;

/* ------------------------------------------------------------------ */
/*  Static version info                                                */
/* ------------------------------------------------------------------ */

static const char *s_protocols[] = {"http", "https", NULL};

static curl_version_info_data s_version_info = {
    CURLVERSION_SIXTH,
    LIBCURL_VERSION,
    LIBCURL_VERSION_NUM,
    "wasm32-emscripten",
    CURL_VERSION_SSL | CURL_VERSION_LIBZ | CURL_VERSION_HTTPS_PROXY,
    "browser-tls",
    0,
    NULL,
    s_protocols,
    /* CURLVERSION_FOURTH */
    NULL,
    0,
    NULL,
    0,
    NULL,
    /* CURLVERSION_FIFTH */
    0,
    NULL,
    0,
    NULL,
    NULL,
    /* CURLVERSION_SIXTH */
    NULL,
    NULL,
};

/* ------------------------------------------------------------------ */
/*  Helpers                                                            */
/* ------------------------------------------------------------------ */

static char *strdup_safe(const char *s)
{
    if (!s)
        return NULL;
    size_t len = strlen(s);
    char *d = (char *)malloc(len + 1);
    if (d)
        memcpy(d, s, len + 1);
    return d;
}

static void handle_reset_fields(CurlHandle *h)
{
    free(h->url);
    h->url = NULL;
    free(h->custom_method);
    h->custom_method = NULL;
    free(h->postfields);
    h->postfields = NULL;
    free(h->useragent);
    h->useragent = NULL;
    free(h->accept_encoding);
    h->accept_encoding = NULL;
    free(h->content_type);
    h->content_type = NULL;
    if (h->headers)
    {
        curl_slist_free_all(h->headers);
        h->headers = NULL;
    }
    h->postfieldsize = -1;
    h->follow_location = 0;
    h->max_redirs = -1;
    h->timeout = 0;
    h->connect_timeout = 0;
    h->nobody = 0;
    h->post = 0;
    h->fail_on_error = 0;
    h->verbose = 0;
    h->write_cb = NULL;
    h->write_data = NULL;
    h->header_cb = NULL;
    h->header_data = NULL;
    h->errorbuffer = NULL;
    h->response_code = 0;
    h->download_size = 0;
}

/* ------------------------------------------------------------------ */
/*  Global init/cleanup (no-op)                                        */
/* ------------------------------------------------------------------ */

CURLcode curl_global_init(long flags)
{
    (void)flags;
    return CURLE_OK;
}

void curl_global_cleanup(void) {}

const char *curl_version(void)
{
    return LIBCURL_VERSION;
}

curl_version_info_data *curl_version_info(CURLversion stamp)
{
    (void)stamp;
    return &s_version_info;
}

/* ------------------------------------------------------------------ */
/*  Easy handle lifecycle                                              */
/* ------------------------------------------------------------------ */

CURL *curl_easy_init(void)
{
    CurlHandle *h = (CurlHandle *)calloc(1, sizeof(CurlHandle));
    if (!h)
        return NULL;
    h->postfieldsize = -1;
    h->max_redirs = -1;
    return (CURL *)h;
}

void curl_easy_cleanup(CURL *handle)
{
    if (!handle)
        return;
    CurlHandle *h = (CurlHandle *)handle;
    handle_reset_fields(h);
    free(h);
}

void curl_easy_reset(CURL *handle)
{
    if (!handle)
        return;
    handle_reset_fields((CurlHandle *)handle);
}

CURL *curl_easy_duphandle(CURL *handle)
{
    if (!handle)
        return NULL;
    CurlHandle *src = (CurlHandle *)handle;
    CurlHandle *dst = (CurlHandle *)calloc(1, sizeof(CurlHandle));
    if (!dst)
        return NULL;
    dst->url = strdup_safe(src->url);
    dst->custom_method = strdup_safe(src->custom_method);
    dst->postfields = strdup_safe(src->postfields);
    dst->useragent = strdup_safe(src->useragent);
    dst->accept_encoding = strdup_safe(src->accept_encoding);
    dst->postfieldsize = src->postfieldsize;
    dst->follow_location = src->follow_location;
    dst->max_redirs = src->max_redirs;
    dst->timeout = src->timeout;
    dst->connect_timeout = src->connect_timeout;
    dst->nobody = src->nobody;
    dst->post = src->post;
    dst->fail_on_error = src->fail_on_error;
    dst->verbose = src->verbose;
    dst->write_cb = src->write_cb;
    dst->write_data = src->write_data;
    dst->header_cb = src->header_cb;
    dst->header_data = src->header_data;
    /* Copy headers linked list */
    struct curl_slist *s = src->headers;
    while (s)
    {
        dst->headers = curl_slist_append(dst->headers, s->data);
        s = s->next;
    }
    return (CURL *)dst;
}

/* ------------------------------------------------------------------ */
/*  Linked list helpers                                                */
/* ------------------------------------------------------------------ */

struct curl_slist *curl_slist_append(struct curl_slist *list, const char *string)
{
    struct curl_slist *node = (struct curl_slist *)malloc(sizeof(struct curl_slist));
    if (!node)
        return list;
    node->data = strdup_safe(string);
    node->next = NULL;
    if (!list)
        return node;
    struct curl_slist *tail = list;
    while (tail->next)
        tail = tail->next;
    tail->next = node;
    return list;
}

void curl_slist_free_all(struct curl_slist *list)
{
    while (list)
    {
        struct curl_slist *next = list->next;
        free(list->data);
        free(list);
        list = next;
    }
}

/* ------------------------------------------------------------------ */
/*  curl_easy_setopt                                                   */
/* ------------------------------------------------------------------ */

CURLcode curl_easy_setopt(CURL *handle, CURLoption option, ...)
{
    if (!handle)
        return CURLE_FAILED_INIT;
    CurlHandle *h = (CurlHandle *)handle;
    va_list ap;
    va_start(ap, option);

    switch (option)
    {
    /* String options */
    case CURLOPT_URL:
        free(h->url);
        h->url = strdup_safe(va_arg(ap, const char *));
        break;
    case CURLOPT_CUSTOMREQUEST:
        free(h->custom_method);
        h->custom_method = strdup_safe(va_arg(ap, const char *));
        break;
    case CURLOPT_POSTFIELDS:
        free(h->postfields);
        h->postfields = strdup_safe(va_arg(ap, const char *));
        break;
    case CURLOPT_USERAGENT:
        free(h->useragent);
        h->useragent = strdup_safe(va_arg(ap, const char *));
        break;
    case CURLOPT_ACCEPT_ENCODING:
        free(h->accept_encoding);
        h->accept_encoding = strdup_safe(va_arg(ap, const char *));
        break;
    case CURLOPT_ERRORBUFFER:
        h->errorbuffer = va_arg(ap, char *);
        break;

    /* Slist options */
    case CURLOPT_HTTPHEADER:
        h->headers = va_arg(ap, struct curl_slist *);
        break;

    /* Long options */
    case CURLOPT_FOLLOWLOCATION:
        h->follow_location = va_arg(ap, long);
        break;
    case CURLOPT_MAXREDIRS:
        h->max_redirs = va_arg(ap, long);
        break;
    case CURLOPT_TIMEOUT:
        h->timeout = va_arg(ap, long);
        break;
    case CURLOPT_CONNECTTIMEOUT:
        h->connect_timeout = va_arg(ap, long);
        break;
    case CURLOPT_NOBODY:
        h->nobody = va_arg(ap, long);
        break;
    case CURLOPT_POST:
        h->post = va_arg(ap, long);
        break;
    case CURLOPT_FAILONERROR:
        h->fail_on_error = va_arg(ap, long);
        break;
    case CURLOPT_VERBOSE:
        h->verbose = va_arg(ap, long);
        break;
    case CURLOPT_POSTFIELDSIZE:
        h->postfieldsize = va_arg(ap, long);
        break;

    /* Function options */
    case CURLOPT_WRITEFUNCTION:
        h->write_cb = va_arg(ap, curl_write_callback);
        break;
    case CURLOPT_WRITEDATA:
        h->write_data = va_arg(ap, void *);
        break;
    case CURLOPT_HEADERFUNCTION:
        h->header_cb = va_arg(ap, curl_write_callback);
        break;
    case CURLOPT_HEADERDATA:
        h->header_data = va_arg(ap, void *);
        break;

    /* Ignored (browser handles TLS, proxy, etc.) */
    case CURLOPT_SSL_VERIFYPEER:
    case CURLOPT_SSL_VERIFYHOST:
    case CURLOPT_CAINFO:
    case CURLOPT_CAPATH:
    case CURLOPT_NOSIGNAL:
    case CURLOPT_NOPROGRESS:
    case CURLOPT_TRANSFER_ENCODING:
    case CURLOPT_PIPEWAIT:
    case CURLOPT_PORT:
    case CURLOPT_PROGRESSFUNCTION:
    case CURLOPT_PROGRESSDATA:
    case CURLOPT_XFERINFOFUNCTION:
    case CURLOPT_POSTFIELDSIZE_LARGE:
    case CURLOPT_DEBUGFUNCTION:
    case CURLOPT_DEBUGDATA:
    case CURLOPT_HTTP_VERSION:
    case CURLOPT_NETRC:
    case CURLOPT_NETRC_FILE:
    case CURLOPT_UPLOAD:
    case CURLOPT_INFILE:
    case CURLOPT_INFILESIZE:
    case CURLOPT_LOW_SPEED_LIMIT:
    case CURLOPT_LOW_SPEED_TIME:
    case CURLOPT_RANGE:
    case CURLOPT_SSLVERSION:
    case CURLOPT_USERPWD:
        va_arg(ap, void *); /* consume the arg */
        break;

    default:
        va_end(ap);
        return CURLE_OK; /* silently ignore unknown options */
    }

    va_end(ap);
    return CURLE_OK;
}

/* ------------------------------------------------------------------ */
/*  curl_easy_perform — IPC via system("__dispatch_curl")              */
/* ------------------------------------------------------------------ */

CURLcode curl_easy_perform(CURL *handle)
{
    if (!handle)
        return CURLE_FAILED_INIT;
    CurlHandle *h = (CurlHandle *)handle;
    if (!h->url)
        return CURLE_URL_MALFORMAT;

    /* Determine HTTP method */
    const char *method = "GET";
    if (h->custom_method)
        method = h->custom_method;
    else if (h->post || h->postfields)
        method = "POST";
    else if (h->nobody)
        method = "HEAD";

    /* Build request file: line1 = "METHOD URL", rest = headers */
    FILE *f = fopen("/tmp/.curl_request", "w");
    if (!f)
        return CURLE_WRITE_ERROR;
    fprintf(f, "%s %s\n", method, h->url);
    if (h->useragent)
        fprintf(f, "User-Agent: %s\n", h->useragent);
    if (h->accept_encoding)
        fprintf(f, "Accept-Encoding: %s\n", h->accept_encoding);
    struct curl_slist *hdr = h->headers;
    while (hdr)
    {
        fprintf(f, "%s\n", hdr->data);
        hdr = hdr->next;
    }
    /* Signal options to JS kernel via pseudo-headers */
    if (h->follow_location)
        fprintf(f, "X-Curl-Follow: 1\n");
    if (h->timeout > 0)
        fprintf(f, "X-Curl-Timeout: %ld\n", h->timeout);
    fclose(f);

    /* Write body if present */
    if (h->postfields)
    {
        long blen = h->postfieldsize >= 0 ? h->postfieldsize : (long)strlen(h->postfields);
        FILE *bf = fopen("/tmp/.curl_request_body", "wb");
        if (bf)
        {
            fwrite(h->postfields, 1, (size_t)blen, bf);
            fclose(bf);
        }
    }
    else
    {
        remove("/tmp/.curl_request_body");
    }

    /* Dispatch — JSPI suspends here, JS kernel does fetch() */
    int rc = system("__dispatch_curl");
    if (rc != 0)
    {
        if (h->errorbuffer)
            snprintf(h->errorbuffer, CURL_ERROR_SIZE, "fetch dispatch failed (rc=%d)", rc);
        return CURLE_COULDNT_CONNECT;
    }

    /* Read response metadata */
    FILE *rf = fopen("/tmp/.curl_response", "r");
    if (!rf)
    {
        if (h->errorbuffer)
            snprintf(h->errorbuffer, CURL_ERROR_SIZE, "no response file from kernel");
        return CURLE_GOT_NOTHING;
    }

    char line[4096];
    int line_no = 0;
    h->response_code = 0;
    free(h->content_type);
    h->content_type = NULL;

    while (fgets(line, sizeof(line), rf))
    {
        /* Strip trailing newline */
        size_t len = strlen(line);
        while (len > 0 && (line[len - 1] == '\n' || line[len - 1] == '\r'))
            line[--len] = '\0';

        if (line_no == 0)
        {
            h->response_code = atol(line);
        }
        else if (len > 0)
        {
            /* Deliver header to callback */
            if (h->header_cb)
            {
                /* fgets stores at most sizeof(line) - 1 payload bytes.
                 * Reserve two more bytes for CRLF; the callback receives a
                 * byte span and does not require a terminating NUL. */
                char hbuf[sizeof(line) + 1];
                memcpy(hbuf, line, len);
                hbuf[len] = '\r';
                hbuf[len + 1] = '\n';
                size_t header_length = len + 2;
                if (h->header_cb(hbuf, 1, header_length, h->header_data) != header_length)
                {
                    fclose(rf);
                    return CURLE_WRITE_ERROR;
                }
            }
            /* Extract content-type */
            if (strncasecmp(line, "content-type:", 13) == 0)
            {
                const char *val = line + 13;
                while (*val == ' ')
                    val++;
                free(h->content_type);
                h->content_type = strdup_safe(val);
            }
        }
        line_no++;
    }
    fclose(rf);

    /* Check fail_on_error */
    if (h->fail_on_error && h->response_code >= 400)
    {
        if (h->errorbuffer)
            snprintf(h->errorbuffer, CURL_ERROR_SIZE, "HTTP %ld", h->response_code);
        return CURLE_HTTP_RETURNED_ERROR;
    }

    /* Read response body and deliver via write callback */
    if (!h->nobody)
    {
        FILE *body = fopen("/tmp/.curl_response_body", "rb");
        if (body)
        {
            char buf[8192];
            size_t n;
            h->download_size = 0;
            while ((n = fread(buf, 1, sizeof(buf), body)) > 0)
            {
                h->download_size += (curl_off_t)n;
                if (h->write_cb)
                {
                    size_t written = h->write_cb(buf, 1, n, h->write_data);
                    if (written != n)
                    {
                        fclose(body);
                        return CURLE_WRITE_ERROR;
                    }
                }
                else
                {
                    /* Default: write to stdout */
                    fwrite(buf, 1, n, stdout);
                }
            }
            fclose(body);
        }
    }

    return CURLE_OK;
}

/* ------------------------------------------------------------------ */
/*  curl_easy_getinfo                                                  */
/* ------------------------------------------------------------------ */

CURLcode curl_easy_getinfo(CURL *handle, CURLINFO info, ...)
{
    if (!handle)
        return CURLE_FAILED_INIT;
    CurlHandle *h = (CurlHandle *)handle;
    va_list ap;
    va_start(ap, info);

    switch (info)
    {
    case CURLINFO_RESPONSE_CODE:
    {
        long *out = va_arg(ap, long *);
        *out = h->response_code;
        break;
    }
    case CURLINFO_CONTENT_TYPE:
    {
        char **out = va_arg(ap, char **);
        *out = h->content_type;
        break;
    }
    case CURLINFO_SIZE_DOWNLOAD_T:
    {
        curl_off_t *out = va_arg(ap, curl_off_t *);
        *out = h->download_size;
        break;
    }
    case CURLINFO_SIZE_DOWNLOAD:
    {
        double *out = va_arg(ap, double *);
        *out = (double)h->download_size;
        break;
    }
    case CURLINFO_HEADER_SIZE:
    {
        long *out = va_arg(ap, long *);
        *out = 0; /* not tracked */
        break;
    }
    default:
    {
        void **out = va_arg(ap, void **);
        *out = NULL;
        break;
    }
    }

    va_end(ap);
    return CURLE_OK;
}

/* ------------------------------------------------------------------ */
/*  curl_easy_strerror                                                 */
/* ------------------------------------------------------------------ */

const char *curl_easy_strerror(CURLcode code)
{
    switch (code)
    {
    case CURLE_OK:
        return "No error";
    case CURLE_UNSUPPORTED_PROTOCOL:
        return "Unsupported protocol";
    case CURLE_FAILED_INIT:
        return "Failed init";
    case CURLE_URL_MALFORMAT:
        return "URL malformat";
    case CURLE_COULDNT_RESOLVE_HOST:
        return "Could not resolve host";
    case CURLE_COULDNT_CONNECT:
        return "Could not connect";
    case CURLE_HTTP_RETURNED_ERROR:
        return "HTTP error";
    case CURLE_WRITE_ERROR:
        return "Write error";
    case CURLE_OUT_OF_MEMORY:
        return "Out of memory";
    case CURLE_OPERATION_TIMEDOUT:
        return "Timeout";
    case CURLE_GOT_NOTHING:
        return "Got nothing";
    case CURLE_ABORTED_BY_CALLBACK:
        return "Aborted by callback";
    default:
        return "Unknown error";
    }
}

/* ------------------------------------------------------------------ */
/*  URL encode/decode + curl_free                                      */
/* ------------------------------------------------------------------ */

char *curl_easy_escape(CURL *handle, const char *string, int length)
{
    (void)handle;
    if (!string || length < 0)
        return NULL;
    size_t slen = length > 0 ? (size_t)length : strlen(string);
    /* Worst case: every byte becomes %XX (3x), plus the terminator. */
    if (slen > (SIZE_MAX - 1) / 3)
        return NULL;
    char *out = (char *)malloc(slen * 3 + 1);
    if (!out)
        return NULL;
    static const char hex[] = "0123456789ABCDEF";
    char *p = out;
    for (size_t i = 0; i < slen; i++)
    {
        unsigned char c = (unsigned char)string[i];
        if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
            (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == '~')
        {
            *p++ = (char)c;
        }
        else
        {
            *p++ = '%';
            *p++ = hex[c >> 4];
            *p++ = hex[c & 0x0F];
        }
    }
    *p = '\0';
    return out;
}

static int hex_digit(char c)
{
    if (c >= '0' && c <= '9')
        return c - '0';
    if (c >= 'a' && c <= 'f')
        return c - 'a' + 10;
    if (c >= 'A' && c <= 'F')
        return c - 'A' + 10;
    return -1;
}

char *curl_easy_unescape(CURL *handle, const char *url, int inlength, int *outlength)
{
    (void)handle;
    if (!url || inlength < 0)
        return NULL;
    size_t slen = inlength > 0 ? (size_t)inlength : strlen(url);
    if (slen == SIZE_MAX)
        return NULL;
    char *out = (char *)malloc(slen + 1);
    if (!out)
        return NULL;
    size_t j = 0;
    for (size_t i = 0; i < slen; i++)
    {
        if (url[i] == '%' && i + 2 < slen)
        {
            int hi = hex_digit(url[i + 1]);
            int lo = hex_digit(url[i + 2]);
            if (hi >= 0 && lo >= 0)
            {
                out[j++] = (char)((hi << 4) | lo);
                i += 2;
                continue;
            }
        }
        out[j++] = url[i];
    }
    out[j] = '\0';
    if (outlength)
    {
        if (j > INT_MAX)
        {
            free(out);
            return NULL;
        }
        *outlength = (int)j;
    }
    return out;
}

void curl_free(void *p)
{
    free(p);
}
