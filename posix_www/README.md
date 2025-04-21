# posix_www

A small example of using ClASP from a simple, bare POSIX application.

The application is mostly C code, except for the contents of `index.clasp`, which require C++ for the multiple overloads of `httpd_send_expr()` used to facilitate `<%= ... %>`

This is primarily provided as a demonstration that ClASP can be run on different platforms, and may be usable as a starter for a browser based user interface internal to an application or as a starting point for an embedded web server.
