# ClASP Suite

ClASP: The C Language ASP and static generator suite

- ClASP is a C/++ oriented HTTP response generator that takes simple ASP-like `<%`, `<%=` and `%>` syntax and generates method calls that sends the HTTP chunked string content over a socket to a browser.
- ClStat is a C/++ oriented static HTTP response generator, and similarly generates method calls to send the static content over a socket to browser
- ClASP-Tree combines the above into a tool that can generate a header file which declares and defines methods that can produce an entire directory tree's associated content

[Clasp-Tree](https://github.com/codewitch-honey-crisis/clasp/tree/master/clasptree) is what you'd normally use to generate an entire webroot folder into a C/++ single header library.

The other two tools generate partial code - just (potentially partial) method bodies and nothing else.

See the README.md at each sub-project for usage
 
- The esp32_www project is an example PlatformIO project written for the ESP-IDF and ESP32s to demonstrate these facilities
- The win32_www project is mostly provided as a proof of this running on disparate platforms, but might be useful for a C/++ application that hosts its own internal web for the UI.
- The posix_www project is an example project written for POSIX systems to demonstrate these facilities
