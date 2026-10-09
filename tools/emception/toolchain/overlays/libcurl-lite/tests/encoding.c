#include <limits.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static const char oversized[] = "simulated oversized string";
static size_t simulated_length;
static int allocation_calls;
static int force_allocation_failure;
static size_t checked_strlen(const char *s) { return s == oversized ? simulated_length : strlen(s); }
static void *checked_malloc(size_t size) { ++allocation_calls; return force_allocation_failure ? NULL : malloc(size); }
#ifdef _WIN32
#define strncasecmp _strnicmp
#endif
#define strlen checked_strlen
#define malloc checked_malloc
#ifndef CURL_LITE_SOURCE
#define CURL_LITE_SOURCE "../src/curl_lite.c"
#endif
#include CURL_LITE_SOURCE
#undef strlen
#undef malloc

static int failures;
#define CHECK(condition, name) do { if (!(condition)) { ++failures; fprintf(stderr, "FAIL %s\n", name); } } while (0)

int main(void) {
 unsigned char bytes[256]; char expected[769]; size_t offset=0;
 const char hex[]="0123456789ABCDEF";
 for(int i=0;i<256;++i) { bytes[i]=(unsigned char)i; if((i>='A'&&i<='Z')||(i>='a'&&i<='z')||(i>='0'&&i<='9')||i=='-'||i=='_'||i=='.'||i=='~') expected[offset++]=(char)i; else {expected[offset++]='%';expected[offset++]=hex[i>>4];expected[offset++]=hex[i&15];}}
 expected[offset]='\0';
 char *encoded=curl_easy_escape(NULL,(const char *)bytes,256);
 CHECK(encoded && strcmp(encoded,expected)==0,"all 256 byte encodings");
 int decoded_length=-1;
 char *decoded=encoded?curl_easy_unescape(NULL,encoded,0,&decoded_length):NULL;
 CHECK(decoded && decoded_length==256 && memcmp(decoded,bytes,256)==0,"binary roundtrip including NUL");
 curl_free(decoded);curl_free(encoded);
 encoded=curl_easy_escape(NULL,"a b",0);CHECK(encoded&&strcmp(encoded,"a%20b")==0,"zero length uses strlen");curl_free(encoded);
 encoded=curl_easy_escape(NULL,"a btail",3);CHECK(encoded&&strcmp(encoded,"a%20b")==0,"explicit input length");curl_free(encoded);
 encoded=curl_easy_escape(NULL,"",0);CHECK(encoded&&encoded[0]=='\0',"empty input");curl_free(encoded);
 const unsigned char utf8[]={0xc3,0xa9}; encoded=curl_easy_escape(NULL,(const char *)utf8,2);CHECK(encoded&&strcmp(encoded,"%C3%A9")==0,"UTF8 is encoded bytewise");curl_free(encoded);
 decoded=curl_easy_unescape(NULL,"a%2Z%",0,&decoded_length);CHECK(decoded&&strcmp(decoded,"a%2Z%")==0,"invalid percent escapes preserved");curl_free(decoded);
 CHECK(curl_easy_escape(NULL,NULL,0)==NULL,"null escape input");
 CHECK(curl_easy_unescape(NULL,NULL,0,&decoded_length)==NULL,"null unescape input");
 allocation_calls=0;encoded=curl_easy_escape(NULL,"abc",-1);CHECK(encoded==NULL&&allocation_calls==0,"negative escape length rejected before allocation");curl_free(encoded);
 allocation_calls=0;decoded=curl_easy_unescape(NULL,"abc",-1,&decoded_length);CHECK(decoded==NULL&&allocation_calls==0,"negative unescape length rejected before allocation");curl_free(decoded);
 force_allocation_failure=1;simulated_length=(SIZE_MAX-1)/3+1;allocation_calls=0;
 CHECK(curl_easy_escape(NULL,oversized,0)==NULL&&allocation_calls==0,"escape capacity overflow rejected before allocation");
 simulated_length=SIZE_MAX;allocation_calls=0;
 CHECK(curl_easy_unescape(NULL,oversized,0,&decoded_length)==NULL&&allocation_calls==0,"unescape terminator overflow rejected before allocation");
 CHECK(curl_easy_escape(NULL,"abc",0)==NULL,"escape allocation failure");
 CHECK(curl_easy_unescape(NULL,"abc",0,&decoded_length)==NULL,"unescape allocation failure");
 printf("curl-lite checked encoding cases: %d failure(s)\n",failures);return failures?1:0;
}
