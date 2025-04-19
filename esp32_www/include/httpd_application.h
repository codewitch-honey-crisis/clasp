#ifndef HTTPD_APPLICATION_H
#define HTTPD_APPLICATION_H
#include <stddef.h>
#include <stdint.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>

extern const float example_star_rating; 
extern const char* episode_title; 
extern const char* show_title;
extern const unsigned char episode_number;
extern const unsigned char season_number;
extern const char* episode_description;
static void httpd_send_block(const char* data, size_t len, void* arg);
static void httpd_send_expr(int expr, void* arg);
static void httpd_send_expr(unsigned char expr, void* arg);
static void httpd_send_expr(float expr, void* arg);
static void httpd_send_expr(const char* expr, void* arg);
extern char enc_rfc3986[256];
extern char enc_html5[256];
static char* httpd_url_encode(char* enc, size_t size, const char* s, const char* table);

#endif // HTTPD_APPLICATION_H