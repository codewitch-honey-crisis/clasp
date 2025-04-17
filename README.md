# ClASP Suite

ClASP: The C Language ASP and static generator suite

- ClASP is a C/++ oriented HTTP response generator that takes simple ASP-like `<%`, `<%=` and `%>` syntax and generates method calls that sends the HTTP chunked string content over a socket to a browser.
- ClStat is a C/++ oriented static HTTP response generator, and similarly generates method calls to send the static content over a socket to browser
- ClASP-Tree combines the above into a tool that can generate a header file which declares and defines methods that can produce an entire directory tree's associated content

See the README.md at each sub-project for usage