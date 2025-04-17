# ClASP-Tree

ClASP-Tree: The C Language website multi-content generator

ClASP-Tree is a C and C++ oriented HTTP response generator that takes a folder of input files and generates a header with method calls to the content over a socket to a browser.

Usage:
```
clasptree 

Usage:

clasptree <input> [ <output> ] [ /block <block> ] [ /expr <expr> ] [ /state <state> ] [ /prefix <prefix> ]
    [ /prologue <prologue> ] [ /epilogue <epilogue> ]

<input>        The root directory of the site. Defaults to the current directory
<output>       The output file to generate. Defaults to <stdout>
<block>        The function call to send a literal block to the client. Defaults to response_block
<expr>         The function call to send an expression to the client. Defaults to response_expr
<state>        The variable name that holds the user state to pass to the response functions. Defaults to response_state
<prefix>       The method prefix to use, if specified.
<prologue>     The file to insert into each method before any code
<epilogue>     The file to insert into each method after any code

clasptree /?

/?             Displays this screen
```
Consider the following
```
clasptree www esp32_www\include\www_content.h /prefix httpd_ /epilogue www_epilogue.h /state resp_arg /block httpd_send_block /expr httpd_send_expr
```
This will take all the content in a folder called www, and generate a header file called www_content.h with it.
It specifies the prefix to use for the generated symbols, an epilogue of code to append in each method, the argument and the methods used to send data.

To understand the details of how it works see the READMEs for CLASP and ClStat_
