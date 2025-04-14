# ClASP

ClASP is a C and C++ oriented HTTP response generator that takes simple ASP-like `<%`, `<%=` and `%>` syntax and generates chunk strings to send over a socket to a browser.

```html
<!DOCTYPE html>
<html>
    <head>
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>Alarm Control Panel</title>
    </head>
    <body>
        <form method="get" action="."><%
           for(size_t i = 0;i<alarm_count;++i) {
            %><label><%=i+1%></label><input name="a" type="checkbox" value="<%=i%>" <%=alarm_values[i]?"checked":""%>/><br /><%
           }%>
            <input type="submit" name="set" value="write"/>
            <input type="submit" name="refresh" value="read"/>
        </form>
    </body>
</html>
```

with the following command arguments: `demo.clasp /state resp_arg /block httpd_send_block /expr httpd_send_expr`

will produce this output:
```cpp
httpd_send_block("E2\r\n<!DOCTYPE html>\r\n<html>\r\n    <head>\r\n        <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\r\n        <title>Alarm Control Panel</title>\r\n    </head>\r\n    <body>\r\n        <form method=\"get\" action=\".\">\r\n", 232, resp_arg);

           for(size_t i = 0;i<alarm_count;++i) {

httpd_send_block("7\r\n<label>\r\n", 12, resp_arg);
httpd_send_expr(i+1, resp_arg);
httpd_send_block("2F\r\n</label><input name=\"a\" type=\"checkbox\" value=\"\r\n", 53, resp_arg);
httpd_send_expr(i, resp_arg);
httpd_send_block("2\r\n\" \r\n", 7, resp_arg);
httpd_send_expr(alarm_values[i]?"checked":"", resp_arg);
httpd_send_block("8\r\n/><br />\r\n", 13, resp_arg);

           }
httpd_send_block("A6\r\n\r\n            <input type=\"submit\" name=\"set\" value=\"write\"/>\r\n            <input type=\"submit\" name=\"refresh\" value=\"read\"/>\r\n        </form>\r\n    </body>\r\n</html>\r\n\r\n", 172, resp_arg);
httpd_send_block("0\r\n\r\n", 5, resp_arg);
```

You then write the simple wrapper functions from above to send data out on a socket. To implement the expression one, you will have to implement a send chunked method yourself. For normal response blocks, the chunking is part of the string, so it can just be sent.

And example of using it is here: https://github.com/codewitch-honey-crisis/core2_alarm/blob/main/src-esp-idf/control-esp-idf.cpp


