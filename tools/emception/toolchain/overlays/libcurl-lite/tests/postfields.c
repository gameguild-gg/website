#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#ifdef _WIN32
#define strncasecmp _strnicmp
#endif

static FILE *body_fixture;
static FILE *request_fixture;
static int method_is_post;
static unsigned char captured_body[64];
static size_t captured_length;
static int dispatch_count;
static int request_count;
static int fail_body_open;
static int fail_body_write;
static int fail_body_close;
static int allocation_countdown = -1;

static void *fixture_malloc(size_t size) {
    if (allocation_countdown == 0) {
        allocation_countdown = -1;
        return NULL;
    }
    if (allocation_countdown > 0) allocation_countdown--;
    return malloc(size);
}

static FILE *fixture_fopen(const char *name, const char *mode) {
    if (strcmp(name, "/tmp/.curl_request_body") == 0 && strcmp(mode, "wb") == 0) {
        if (fail_body_open) return NULL;
        body_fixture = tmpfile();
        return body_fixture;
    }
    if (strcmp(name, "/tmp/.curl_request") == 0 && strcmp(mode, "w") == 0) {
        request_count++;
        request_fixture = tmpfile();
        return request_fixture;
    }
    if (strcmp(name, "/tmp/.curl_response_body") == 0 && strcmp(mode, "rb") == 0) {
        return tmpfile();
    }
    if (strcmp(name, "/tmp/.curl_response") == 0 && strcmp(mode, "r") == 0) {
        FILE *response = tmpfile();
        if (!response) return NULL;
        fputs("200\n", response);
        rewind(response);
        return response;
    }
    return NULL;
}

static size_t fixture_fwrite(const void *data, size_t size, size_t count, FILE *stream) {
    if (stream == body_fixture) {
        if (fail_body_write) return 0;
        if (size != 1 || count > sizeof(captured_body)) return 0;
        /* Read every byte promised by perform; ASan catches a short backing allocation. */
        if (count > 0) memcpy(captured_body, data, count);
        captured_length = count;
    }
    return fwrite(data, size, count, stream);
}

static int fixture_fclose(FILE *stream) {
    if (stream == request_fixture) {
        char first_line[128];
        fflush(stream);
        rewind(stream);
        method_is_post = fgets(first_line, sizeof(first_line), stream) != NULL &&
                         strncmp(first_line, "POST ", 5) == 0;
        request_fixture = NULL;
    }
    int is_body = stream == body_fixture;
    if (is_body) body_fixture = NULL;
    int result = fclose(stream);
    return is_body && fail_body_close ? EOF : result;
}

static int fixture_remove(const char *name) {
    (void)name;
    return 0;
}

static int dispatch_curl(const char *command) {
    dispatch_count++;
    return strcmp(command, "__dispatch_curl") == 0 ? 0 : 1;
}

#define malloc fixture_malloc
#define fopen fixture_fopen
#define fwrite fixture_fwrite
#define fclose fixture_fclose
#define remove fixture_remove
#define system dispatch_curl
#ifndef CURL_LITE_SOURCE
#define CURL_LITE_SOURCE "../src/curl_lite.c"
#endif
#include CURL_LITE_SOURCE
#undef malloc
#undef fopen
#undef fwrite
#undef fclose
#undef remove
#undef system

_Static_assert(sizeof(curl_off_t) == 8, "curl_off_t must retain the signed 64-bit libcurl ABI");

static void reset_capture(void) {
    captured_length = 0;
    dispatch_count = 0;
    request_count = 0;
    method_is_post = 0;
    fail_body_open = fail_body_write = fail_body_close = 0;
    allocation_countdown = -1;
}

static CURL *create_handle(void) {
    CURL *handle = curl_easy_init();
    if (!handle) return NULL;
    if (curl_easy_setopt(handle, CURLOPT_URL, "https://example.invalid/body-regression") != CURLE_OK) {
        curl_easy_cleanup(handle);
        return NULL;
    }
    return handle;
}

static CURLcode set_length(CURL *handle, int large, size_t length) {
    return large ? curl_easy_setopt(handle, CURLOPT_POSTFIELDSIZE_LARGE, (curl_off_t)length)
                 : curl_easy_setopt(handle, CURLOPT_POSTFIELDSIZE, (long)length);
}

static int check_borrowed(int large, int size_first, int embedded_zero, int zero_length, int duplicate) {
    reset_capture();
    unsigned char *data = (unsigned char *)malloc(3);
    CURL *handle = create_handle();
    if (!data || !handle) { free(data); curl_easy_cleanup(handle); return 1; }
    data[0] = 'a'; data[1] = embedded_zero ? 0 : 'b'; data[2] = 'c';
    size_t length = zero_length ? 0 : 3;
    if ((size_first && set_length(handle, large, length) != CURLE_OK) ||
        curl_easy_setopt(handle, CURLOPT_POSTFIELDS, data) != CURLE_OK ||
        (!size_first && set_length(handle, large, length) != CURLE_OK)) {
        free(data); curl_easy_cleanup(handle); return 1;
    }
    if (duplicate) {
        CURL *copy = curl_easy_duphandle(handle);
        curl_easy_cleanup(handle);
        handle = copy;
    }
    data[0] = 'z';
    CURLcode result = handle ? curl_easy_perform(handle) : CURLE_FAILED_INIT;
    int failure = result != CURLE_OK || dispatch_count != 1 || !method_is_post || captured_length != length ||
                  (length > 0 && memcmp(captured_body, data, length) != 0);
    curl_easy_cleanup(handle);
    free(data);
    return failure;
}

#ifndef CURL_LITE_BASELINE_ONLY
_Static_assert((curl_off_t)-1 < 0, "curl_off_t must support the -1 length sentinel");

static int check_owned(int large, int embedded_zero, int duplicate) {
    reset_capture();
    unsigned char expected[3] = {'a', embedded_zero ? 0 : 'b', 'c'};
    unsigned char *data = (unsigned char *)malloc(3);
    CURL *handle = create_handle();
    if (!data || !handle) { free(data); curl_easy_cleanup(handle); return 1; }
    memcpy(data, expected, sizeof(expected));
    if (set_length(handle, large, 3) != CURLE_OK ||
        curl_easy_setopt(handle, CURLOPT_COPYPOSTFIELDS, data) != CURLE_OK) {
        free(data); curl_easy_cleanup(handle); return 1;
    }
    memset(data, 'x', 3);
    free(data);
    if (duplicate) {
        CURL *copy = curl_easy_duphandle(handle);
        curl_easy_cleanup(handle);
        handle = copy;
    }
    CURLcode result = handle ? curl_easy_perform(handle) : CURLE_FAILED_INIT;
    int failure = result != CURLE_OK || dispatch_count != 1 || !method_is_post || captured_length != 3 ||
                  memcmp(captured_body, expected, 3) != 0;
    curl_easy_cleanup(handle);
    return failure;
}

static int check_size_guards(void) {
    reset_capture();
    CURL *handle = create_handle();
    if (!handle) return 1;
    const char data[3] = {'a', 0, 'b'};
    int failure = set_length(handle, 1, 3) != CURLE_OK ||
                  curl_easy_setopt(handle, CURLOPT_COPYPOSTFIELDS, data) != CURLE_OK ||
                  curl_easy_setopt(handle, CURLOPT_POSTFIELDSIZE, -2L) != CURLE_BAD_FUNCTION_ARGUMENT ||
                  curl_easy_setopt(handle, CURLOPT_POSTFIELDSIZE_LARGE, (curl_off_t)-2) != CURLE_BAD_FUNCTION_ARGUMENT;
    failure |= curl_easy_perform(handle) != CURLE_OK || captured_length != 3;
    reset_capture();
    failure |= curl_easy_setopt(handle, CURLOPT_POSTFIELDSIZE_LARGE, (curl_off_t)INT64_MAX) != CURLE_OK;
    failure |= curl_easy_perform(handle) != CURLE_BAD_FUNCTION_ARGUMENT || dispatch_count != 0 || request_count != 0;
    curl_easy_cleanup(handle);
    return failure;
}

static int check_owned_allocation_failure(int duplicate) {
    reset_capture();
    CURL *handle = create_handle();
    if (!handle) return 1;
    const char data[3] = {'a', 0, 'b'};
    int failure = set_length(handle, 0, 3) != CURLE_OK ||
                  curl_easy_setopt(handle, CURLOPT_COPYPOSTFIELDS, data) != CURLE_OK;
    allocation_countdown = duplicate ? 1 : 0;
    if (duplicate) {
        CURL *copy = curl_easy_duphandle(handle);
        failure |= copy != NULL;
        curl_easy_cleanup(copy);
    } else {
        failure |= curl_easy_setopt(handle, CURLOPT_COPYPOSTFIELDS, data) != CURLE_OUT_OF_MEMORY;
    }
    failure |= curl_easy_perform(handle) != CURLE_OK || captured_length != 3 ||
               memcmp(captured_body, data, 3) != 0;
    curl_easy_cleanup(handle);
    return failure;
}

static int check_body_io_failure(int kind) {
    reset_capture();
    CURL *handle = create_handle();
    if (!handle) return 1;
    const char data[3] = {'a', 0, 'b'};
    int failure = set_length(handle, 0, 3) != CURLE_OK ||
                  curl_easy_setopt(handle, CURLOPT_POSTFIELDS, data) != CURLE_OK;
    fail_body_open = kind == 0;
    fail_body_write = kind == 1;
    fail_body_close = kind == 2;
    failure |= curl_easy_perform(handle) != CURLE_WRITE_ERROR || dispatch_count != 0;
    curl_easy_cleanup(handle);
    return failure;
}

static int check_replace_reset_and_implicit_text(void) {
    reset_capture();
    CURL *handle = create_handle();
    if (!handle) return 1;
    const char borrowed[3] = {'a', 0, 'b'};
    int failure = curl_easy_setopt(handle, CURLOPT_COPYPOSTFIELDS, "text") != CURLE_OK ||
                  curl_easy_perform(handle) != CURLE_OK || captured_length != 4 ||
                  memcmp(captured_body, "text", 4) != 0;
    reset_capture();
    failure |= set_length(handle, 0, 3) != CURLE_OK ||
               curl_easy_setopt(handle, CURLOPT_POSTFIELDS, borrowed) != CURLE_OK ||
               curl_easy_perform(handle) != CURLE_OK || captured_length != 3 ||
               memcmp(captured_body, borrowed, 3) != 0;
    curl_easy_reset(handle);
    failure |= ((CurlHandle *)handle)->postfields != NULL ||
               ((CurlHandle *)handle)->owned_postfields != NULL ||
               ((CurlHandle *)handle)->postfieldsize != -1;
    curl_easy_cleanup(handle);
    return failure;
}
#endif

int main(void) {
    int failures = 0;
    int cases = 0;
#define RUN_CASE(expression) do { cases++; failures += (expression); } while (0)
#ifdef CURL_LITE_BASELINE_ONLY
    RUN_CASE(check_borrowed(0, 1, 1, 0, 0));
#else
    for (int large = 0; large < 2; large++) {
        for (int first = 0; first < 2; first++) {
            for (int binary = 0; binary < 2; binary++)
                RUN_CASE(check_borrowed(large, first, binary, 0, 0));
        }
        RUN_CASE(check_borrowed(large, 1, 0, 1, 0));
        RUN_CASE(check_borrowed(large, 1, 1, 0, 1));
        for (int binary = 0; binary < 2; binary++)
            RUN_CASE(check_owned(large, binary, 0));
        RUN_CASE(check_owned(large, 1, 1));
    }
    RUN_CASE(check_size_guards());
    RUN_CASE(check_owned_allocation_failure(0));
    RUN_CASE(check_owned_allocation_failure(1));
    for (int kind = 0; kind < 3; kind++) RUN_CASE(check_body_io_failure(kind));
    RUN_CASE(check_replace_reset_and_implicit_text());
#endif
#undef RUN_CASE
    printf("curl-lite postfields regression: %d case(s), %d failure(s)\n", cases, failures);
    return failures ? 1 : 0;
}
