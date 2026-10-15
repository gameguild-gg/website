/**
 * libcurl-lite — Minimal libcurl-compatible API for browser WASM.
 *
 * This header provides a subset of the real libcurl API, with ABI-compatible
 * enum values. The implementation routes all HTTP requests through the
 * browser's fetch() API via Emscripten's system() → JSPI bridge.
 *
 * Supported features:
 *   - GET / HEAD / POST with custom method override
 *   - Custom headers (curl_slist)
 *   - Write callback (CURLOPT_WRITEFUNCTION / CURLOPT_WRITEDATA)
 *   - Header callback (CURLOPT_HEADERFUNCTION / CURLOPT_HEADERDATA)
 *   - Follow redirects (CURLOPT_FOLLOWLOCATION)
 *   - Timeout (CURLOPT_TIMEOUT / CURLOPT_CONNECTTIMEOUT)
 *   - Error buffer (CURLOPT_ERRORBUFFER)
 *   - Response info (CURLINFO_RESPONSE_CODE, CURLINFO_CONTENT_TYPE)
 *
 * Not supported (no-op or ignored):
 *   - TLS/SSL options (browser handles TLS transparently)
 *   - Proxy, cookies, auth
 *   - Multi interface
 *   - FTP, SFTP, SCP, etc.
 */

#ifndef CURLINC_CURL_H
#define CURLINC_CURL_H

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C"
{
#endif

    /* ------------------------------------------------------------------ */
    /*  Version info                                                       */
    /* ------------------------------------------------------------------ */

#define LIBCURL_VERSION "8.20.0-lite"
#define LIBCURL_VERSION_NUM 0x081400
#define LIBCURL_VERSION_MAJOR 8
#define LIBCURL_VERSION_MINOR 20
#define LIBCURL_VERSION_PATCH 0

    /* ------------------------------------------------------------------ */
    /*  Types                                                              */
    /* ------------------------------------------------------------------ */

    /** Opaque handle for an "easy" session. */
    typedef void CURL;

    /** Linked list for headers, recipients, etc. */
    struct curl_slist
    {
        char *data;
        struct curl_slist *next;
    };

    /** Signed 64-bit byte count used by libcurl large-size options and info. */
    typedef int64_t curl_off_t;

    /* ------------------------------------------------------------------ */
    /*  Callback signatures                                                */
    /* ------------------------------------------------------------------ */

    /**
     * Write callback: receives data from the server.
     * Must return the number of bytes handled. If != size*nmemb, transfer aborts.
     */
    typedef size_t (*curl_write_callback)(char *ptr, size_t size, size_t nmemb, void *userdata);

    /**
     * Progress callback (deprecated form).
     */
    typedef int (*curl_progress_callback)(void *clientp, double dltotal, double dlnow,
                                          double ultotal, double ulnow);

    /**
     * Transfer info callback (modern form).
     */
    typedef int (*curl_xferinfo_callback)(void *clientp, curl_off_t dltotal, curl_off_t dlnow,
                                          curl_off_t ultotal, curl_off_t ulnow);

    /* ------------------------------------------------------------------ */
    /*  CURLcode — error codes (ABI-compatible with real libcurl)          */
    /* ------------------------------------------------------------------ */

    typedef enum
    {
        CURLE_OK = 0,
        CURLE_UNSUPPORTED_PROTOCOL = 1,
        CURLE_FAILED_INIT = 2,
        CURLE_URL_MALFORMAT = 3,
        CURLE_COULDNT_RESOLVE_PROXY = 5,
        CURLE_COULDNT_RESOLVE_HOST = 6,
        CURLE_COULDNT_CONNECT = 7,
        CURLE_REMOTE_ACCESS_DENIED = 9,
        CURLE_HTTP_RETURNED_ERROR = 22,
        CURLE_WRITE_ERROR = 23,
        CURLE_READ_ERROR = 26,
        CURLE_OUT_OF_MEMORY = 27,
        CURLE_OPERATION_TIMEDOUT = 28,
        CURLE_SSL_CONNECT_ERROR = 35,
        CURLE_ABORTED_BY_CALLBACK = 42,
        CURLE_BAD_FUNCTION_ARGUMENT = 43,
        CURLE_NOT_BUILT_IN = 4,
        CURLE_GOT_NOTHING = 52,
        CURLE_SEND_ERROR = 55,
        CURLE_RECV_ERROR = 56,
        CURLE_PEER_FAILED_VERIFICATION = 60,
    } CURLcode;

/* ------------------------------------------------------------------ */
/*  CURLoption — option IDs (ABI-compatible with real libcurl)         */
/* ------------------------------------------------------------------ */

/** Base type tags for option numbering. */
#define CURLOPTTYPE_LONG 0
#define CURLOPTTYPE_OBJECTPOINT 10000
#define CURLOPTTYPE_FUNCTIONPOINT 20000
#define CURLOPTTYPE_OFF_T 30000
#define CURLOPTTYPE_STRINGPOINT CURLOPTTYPE_OBJECTPOINT
#define CURLOPTTYPE_SLISTPOINT CURLOPTTYPE_OBJECTPOINT
#define CURLOPTTYPE_CBPOINT CURLOPTTYPE_OBJECTPOINT

    typedef enum
    {
        /* --- String / object options --- */
        CURLOPT_WRITEDATA = CURLOPTTYPE_OBJECTPOINT + 1,         /* 10001 */
        CURLOPT_URL = CURLOPTTYPE_OBJECTPOINT + 2,               /* 10002 */
        CURLOPT_ERRORBUFFER = CURLOPTTYPE_OBJECTPOINT + 10,      /* 10010 */
        CURLOPT_POSTFIELDS = CURLOPTTYPE_OBJECTPOINT + 15,       /* 10015 */
        CURLOPT_USERAGENT = CURLOPTTYPE_OBJECTPOINT + 18,        /* 10018 */
        CURLOPT_HTTPHEADER = CURLOPTTYPE_SLISTPOINT + 23,        /* 10023 */
        CURLOPT_HEADERDATA = CURLOPTTYPE_OBJECTPOINT + 29,       /* 10029 */
        CURLOPT_CUSTOMREQUEST = CURLOPTTYPE_OBJECTPOINT + 36,    /* 10036 */
        CURLOPT_PROGRESSDATA = CURLOPTTYPE_OBJECTPOINT + 57,     /* 10057 */
        CURLOPT_CAINFO = CURLOPTTYPE_OBJECTPOINT + 65,           /* 10065 */
        CURLOPT_CAPATH = CURLOPTTYPE_OBJECTPOINT + 97,           /* 10097 */
        CURLOPT_ACCEPT_ENCODING = CURLOPTTYPE_OBJECTPOINT + 102, /* 10102 */
        CURLOPT_COPYPOSTFIELDS = CURLOPTTYPE_OBJECTPOINT + 165,   /* 10165 */
        CURLOPT_NETRC_FILE = CURLOPTTYPE_OBJECTPOINT + 118,      /* 10118 */
        CURLOPT_USERPWD = CURLOPTTYPE_OBJECTPOINT + 5,           /* 10005 */
        CURLOPT_RANGE = CURLOPTTYPE_OBJECTPOINT + 7,             /* 10007 */
        CURLOPT_INFILE = CURLOPTTYPE_OBJECTPOINT + 9,            /* 10009 */
        CURLOPT_DEBUGDATA = CURLOPTTYPE_OBJECTPOINT + 95,        /* 10095 */

        /* --- Long options --- */
        CURLOPT_PORT = CURLOPTTYPE_LONG + 3,                /* 3 */
        CURLOPT_TIMEOUT = CURLOPTTYPE_LONG + 13,            /* 13 */
        CURLOPT_VERBOSE = CURLOPTTYPE_LONG + 41,            /* 41 */
        CURLOPT_NOPROGRESS = CURLOPTTYPE_LONG + 43,         /* 43 */
        CURLOPT_NOBODY = CURLOPTTYPE_LONG + 44,             /* 44 */
        CURLOPT_FAILONERROR = CURLOPTTYPE_LONG + 45,        /* 45 */
        CURLOPT_POST = CURLOPTTYPE_LONG + 47,               /* 47 */
        CURLOPT_FOLLOWLOCATION = CURLOPTTYPE_LONG + 52,     /* 52 */
        CURLOPT_POSTFIELDSIZE = CURLOPTTYPE_LONG + 60,      /* 60 */
        CURLOPT_SSL_VERIFYPEER = CURLOPTTYPE_LONG + 64,     /* 64 */
        CURLOPT_MAXREDIRS = CURLOPTTYPE_LONG + 68,          /* 68 */
        CURLOPT_INFILESIZE = CURLOPTTYPE_LONG + 14,         /* 14 */
        CURLOPT_LOW_SPEED_LIMIT = CURLOPTTYPE_LONG + 19,    /* 19 */
        CURLOPT_LOW_SPEED_TIME = CURLOPTTYPE_LONG + 20,     /* 20 */
        CURLOPT_SSLVERSION = CURLOPTTYPE_LONG + 32,         /* 32 */
        CURLOPT_UPLOAD = CURLOPTTYPE_LONG + 46,             /* 46 */
        CURLOPT_NETRC = CURLOPTTYPE_LONG + 51,              /* 51 */
        CURLOPT_CONNECTTIMEOUT = CURLOPTTYPE_LONG + 78,     /* 78 */
        CURLOPT_SSL_VERIFYHOST = CURLOPTTYPE_LONG + 81,     /* 81 */
        CURLOPT_HTTP_VERSION = CURLOPTTYPE_LONG + 84,       /* 84 */
        CURLOPT_NOSIGNAL = CURLOPTTYPE_LONG + 99,           /* 99 */
        CURLOPT_TRANSFER_ENCODING = CURLOPTTYPE_LONG + 207, /* 207 */
        CURLOPT_PIPEWAIT = CURLOPTTYPE_LONG + 237,          /* 237 */

        /* --- Function pointer options --- */
        CURLOPT_WRITEFUNCTION = CURLOPTTYPE_FUNCTIONPOINT + 11,     /* 20011 */
        CURLOPT_PROGRESSFUNCTION = CURLOPTTYPE_FUNCTIONPOINT + 56,  /* 20056 */
        CURLOPT_HEADERFUNCTION = CURLOPTTYPE_FUNCTIONPOINT + 79,    /* 20079 */
        CURLOPT_DEBUGFUNCTION = CURLOPTTYPE_FUNCTIONPOINT + 94,     /* 20094 */
        CURLOPT_XFERINFOFUNCTION = CURLOPTTYPE_FUNCTIONPOINT + 219, /* 20219 */

        /* --- Off_t options --- */
        CURLOPT_POSTFIELDSIZE_LARGE = CURLOPTTYPE_OFF_T + 120, /* 30120 */
    } CURLoption;

/* Alias — same value as CURLOPT_PROGRESSDATA */
#define CURLOPT_XFERINFODATA CURLOPT_PROGRESSDATA

    /* ------------------------------------------------------------------ */
    /*  CURLINFO — getinfo IDs (ABI-compatible with real libcurl)          */
    /* ------------------------------------------------------------------ */

#define CURLINFO_STRING 0x100000
#define CURLINFO_LONG 0x200000
#define CURLINFO_DOUBLE 0x300000
#define CURLINFO_SLIST 0x400000
#define CURLINFO_OFF_T 0x600000

    typedef enum
    {
        CURLINFO_RESPONSE_CODE = CURLINFO_LONG + 2,      /* 0x200002 */
        CURLINFO_CONTENT_TYPE = CURLINFO_STRING + 18,    /* 0x100012 */
        CURLINFO_SIZE_DOWNLOAD_T = CURLINFO_OFF_T + 32,  /* 0x600020 */
        CURLINFO_SPEED_DOWNLOAD_T = CURLINFO_OFF_T + 33, /* 0x600021 */
        /* Legacy aliases */
        CURLINFO_SIZE_DOWNLOAD = CURLINFO_DOUBLE + 8,  /* 0x300008 */
        CURLINFO_SPEED_DOWNLOAD = CURLINFO_DOUBLE + 9, /* 0x300009 */
        CURLINFO_HEADER_SIZE = CURLINFO_LONG + 11,     /* 0x20000B */
    } CURLINFO;

    /* ------------------------------------------------------------------ */
    /*  Constants                                                          */
    /* ------------------------------------------------------------------ */

#define CURL_ERROR_SIZE 256
#define CURL_GLOBAL_DEFAULT 3
#define CURL_GLOBAL_ALL 3
#define CURL_GLOBAL_SSL 1
#define CURL_GLOBAL_WIN32 2
#define CURL_GLOBAL_NOTHING 0

#define CURL_WRITEFUNC_ERROR 0xFFFFFFFF

    /* SSL version constants */
#define CURL_SSLVERSION_DEFAULT 0
#define CURL_SSLVERSION_TLSv1 1
#define CURL_SSLVERSION_SSLv2 2
#define CURL_SSLVERSION_SSLv3 3
#define CURL_SSLVERSION_TLSv1_0 4
#define CURL_SSLVERSION_TLSv1_1 5
#define CURL_SSLVERSION_TLSv1_2 6
#define CURL_SSLVERSION_TLSv1_3 7
#define CURL_SSLVERSION_LAST 8

    /* Netrc level constants */
#define CURL_NETRC_IGNORED 0
#define CURL_NETRC_OPTIONAL 1
#define CURL_NETRC_REQUIRED 2
#define CURL_NETRC_LAST 3

    /* HTTP version constants */
#define CURL_HTTP_VERSION_NONE 0
#define CURL_HTTP_VERSION_1_0 1
#define CURL_HTTP_VERSION_1_1 2
#define CURL_HTTP_VERSION_2_0 3
#define CURL_HTTP_VERSION_2TLS 4
#define CURL_HTTP_VERSION_2_PRIOR_KNOWLEDGE 5
#define CURL_HTTP_VERSION_3 30

    /* Debug info types */
    typedef enum
    {
        CURLINFO_TEXT = 0,
        CURLINFO_HEADER_IN = 1,
        CURLINFO_HEADER_OUT = 2,
        CURLINFO_DATA_IN = 3,
        CURLINFO_DATA_OUT = 4,
        CURLINFO_SSL_DATA_IN = 5,
        CURLINFO_SSL_DATA_OUT = 6,
    } curl_infotype;

    /* SSL backend IDs */
    typedef enum
    {
        CURLSSLBACKEND_NONE = 0,
        CURLSSLBACKEND_OPENSSL = 1,
        CURLSSLBACKEND_GNUTLS = 2,
        CURLSSLBACKEND_SECURETRANSPORT = 9,
        CURLSSLBACKEND_DARWINSSL = CURLSSLBACKEND_SECURETRANSPORT,
    } curl_sslbackend;

    /* ------------------------------------------------------------------ */
    /*  Version info struct                                                */
    /* ------------------------------------------------------------------ */

    typedef enum
    {
        CURLVERSION_FIRST = 0,
        CURLVERSION_SECOND = 1,
        CURLVERSION_THIRD = 2,
        CURLVERSION_FOURTH = 3,
        CURLVERSION_FIFTH = 4,
        CURLVERSION_SIXTH = 5,
        CURLVERSION_NOW = CURLVERSION_SIXTH,
    } CURLversion;

    typedef struct
    {
        CURLversion age;
        const char *version;
        unsigned int version_num;
        const char *host;
        int features;
        const char *ssl_version;
        long ssl_version_num;
        const char *libz_version;
        const char *const *protocols;
        /* CURLVERSION_FOURTH fields */
        const char *ares;
        int ares_num;
        const char *libidn;
        int iconv_ver_num;
        const char *libssh_version;
        /* CURLVERSION_FIFTH fields */
        unsigned int brotli_ver_num;
        const char *brotli_version;
        unsigned int nghttp2_ver_num;
        const char *nghttp2_version;
        const char *quic_version;
        /* CURLVERSION_SIXTH fields */
        const char *cainfo;
        const char *capath;
    } curl_version_info_data;

/* Feature bits */
#define CURL_VERSION_IPV6 (1 << 0)
#define CURL_VERSION_KERBEROS4 (1 << 1)
#define CURL_VERSION_SSL (1 << 2)
#define CURL_VERSION_LIBZ (1 << 3)
#define CURL_VERSION_HTTPS_PROXY (1 << 21)

    /* ------------------------------------------------------------------ */
    /*  Function declarations                                              */
    /* ------------------------------------------------------------------ */

    /** Global init/cleanup (no-op in browser). */
    CURLcode curl_global_init(long flags);
    void curl_global_cleanup(void);

    /** Easy interface. */
    CURL *curl_easy_init(void);
    void curl_easy_cleanup(CURL *handle);
    void curl_easy_reset(CURL *handle);
    CURL *curl_easy_duphandle(CURL *handle);
    CURLcode curl_easy_setopt(CURL *handle, CURLoption option, ...);
    CURLcode curl_easy_perform(CURL *handle);
    CURLcode curl_easy_getinfo(CURL *handle, CURLINFO info, ...);
    const char *curl_easy_strerror(CURLcode code);

    /** Linked list helpers. */
    struct curl_slist *curl_slist_append(struct curl_slist *list, const char *string);
    void curl_slist_free_all(struct curl_slist *list);

    /** Version info. */
    const char *curl_version(void);
    curl_version_info_data *curl_version_info(CURLversion stamp);

    /** URL-encode/decode (minimal). */
    char *curl_easy_escape(CURL *handle, const char *string, int length);
    char *curl_easy_unescape(CURL *handle, const char *url, int inlength, int *outlength);
    void curl_free(void *p);

#ifdef __cplusplus
}
#endif

#endif /* CURLINC_CURL_H */
