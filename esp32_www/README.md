# esp32_www

A small example of using ClASP from a simple, bare esp32 esp-idf application.

The application is mostly C code, except for the contents of `index.clasp`, which require C++ for the multiple overloads of `httpd_send_expr()` used to facilitate `<%= ... %>`

This is primarily provided as a demonstration of using ClASP with the widely available ESP32 and the ESP-IDF.

