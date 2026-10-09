#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#ifdef _WIN32
#define strncasecmp _strnicmp
#endif

static FILE *response_fixture;

static FILE *fixture_fopen(const char *path, const char *mode) {
    if (strcmp(path, "/tmp/.curl_response") == 0 && strcmp(mode, "r") == 0) {
        rewind(response_fixture);
        return response_fixture;
    }
    if (strcmp(path, "/tmp/.curl_request") == 0 && strcmp(mode, "w") == 0) {
        return tmpfile();
    }
    return NULL;
}

static int fixture_remove(const char *path) {
    (void)path;
    return 0;
}

static int dispatch_curl(const char *command) {
    return strcmp(command, "__dispatch_curl") == 0 ? 0 : 1;
}
#define system dispatch_curl
#define fopen fixture_fopen
#define remove fixture_remove
#ifndef CURL_LITE_SOURCE
#define CURL_LITE_SOURCE "../src/curl_lite.c"
#endif
#include CURL_LITE_SOURCE
#undef system
#undef fopen
#undef remove

struct HeaderCapture {
    size_t calls;
    size_t bytes;
    int abort_transfer;
    int malformed;
};

static size_t capture_header(char *buffer, size_t size, size_t count, void *user_data) {
    struct HeaderCapture *capture = (struct HeaderCapture *)user_data;
    size_t length = size * count;
    /* Copy every promised byte, so ASan detects any callback overread. */
    char *copy = (char *)malloc(length);
    if (!copy) {
        return 0;
    }
    memcpy(copy, buffer, length);
    if (length < 2 || copy[length - 2] != '\r' || copy[length - 1] != '\n') {
        capture->malformed = 1;
    }
    free(copy);
    capture->calls++;
    capture->bytes += length;
    return capture->abort_transfer ? 0 : length;
}

static int check_header(size_t payload_length, int abort_transfer) {
    FILE *response = tmpfile();
    if (!response) {
        return 1;
    }
    fputs("200\n", response);
    for (size_t index = 0; index < payload_length; index++) {
        fputc('a', response);
    }
    fputc('\n', response);
    fflush(response);
    response_fixture = response;

    struct HeaderCapture capture = {0, 0, abort_transfer, 0};
    CURL *handle = curl_easy_init();
    if (!handle) {
        fclose(response);
        return 1;
    }
    curl_easy_setopt(handle, CURLOPT_URL, "https://example.invalid/header-regression");
    curl_easy_setopt(handle, CURLOPT_NOBODY, 1L);
    curl_easy_setopt(handle, CURLOPT_HEADERFUNCTION, capture_header);
    curl_easy_setopt(handle, CURLOPT_HEADERDATA, &capture);
    CURLcode result = curl_easy_perform(handle);
    curl_easy_cleanup(handle);
    response_fixture = NULL;
    CURLcode expected = abort_transfer ? CURLE_WRITE_ERROR : CURLE_OK;
    if (result != expected || capture.calls == 0 || capture.malformed ||
        (!abort_transfer && capture.bytes != payload_length + capture.calls * 2)) {
        fprintf(stderr, "FAIL header payload=%zu abort=%d result=%d calls=%zu bytes=%zu malformed=%d\n",
                payload_length, abort_transfer, result, capture.calls, capture.bytes, capture.malformed);
        return 1;
    }
    return 0;
}

int main(void) {
    const size_t lengths[] = {1, 4093, 4094, 4095, 8191};
    int failures = 0;
    for (size_t index = 0; index < sizeof(lengths) / sizeof(lengths[0]); index++) {
        failures += check_header(lengths[index], 0);
    }
    failures += check_header(32, 1);
    printf("curl-lite header regression: %d failure(s)\n", failures);
    return failures ? 1 : 0;
}
