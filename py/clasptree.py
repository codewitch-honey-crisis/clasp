import argparse
from luthor import CodepointDfa, DfaState, Builder, compile_dfa, fsm_escape
from urllib.parse import quote
import os
from glob import glob
import subprocess
line = 1
firstEmit = 1
indent = ""
eol = ""
output = None
cmdargParser = argparse.ArgumentParser("clasptree")
cmdargParser.add_argument("input", help="The root directory of the site to generate code for", type=str)
cmdargParser.add_argument("-o","--output", help="The output file to produce", type=str,required=False)
cmdargParser.add_argument("-b","--block", help="The function call to send a literal block to the client", type=str,required=False,default="response_block")
cmdargParser.add_argument("-e", "--expr", help = "The function call to send an expression to the client",type=str,required=False,default="response_expr")
cmdargParser.add_argument("-s","--state", help = "The variable name that holds the user state to pass to the response functions",required=False,default="response_state",type=str)
cmdargParser.add_argument("-n","--nostatus", required= False, help = "Suppress the status line",action="store_true")
cmdargParser.add_argument("-p","--prefix", help = "The method prefix to use",required=False,type=str)
cmdargParser.add_argument("-x","--extra", help = "The extra include file to place in the output",required=False,type=str)
cmdargParser.add_argument("-P","--prologue", help = "The file to insert into each method before any code",required=False,type=str)
cmdargParser.add_argument("-E","--epilogue", help = "The file to insert into each method after any code",required=False,type=str)
cmdargParser.add_argument("-H","--handlers", required= False, help = "Indicates whether to generate no handler entries (none), default entries (default) or extended (extended) handlers. None doesn't emit any. Default emits them in accordance with their paths, plus resoving indexes based on <index>. Extended does this and also adds path/ trailing handlers",default="auto",type=str)
cmdargParser.add_argument("-i","--index", required= False, help = "Generate / default handlers for files matching this wildcard. Defaults to \"index.*\"",type=str)
cmdargParser.add_argument("-m","--handlerfsm", required= False, help = "Generate a finite state machine that can be used for matching handlers",action="store_true")
cmdargParser.add_argument("-u","--urlmap", help = "Generates handler mappings from a map file. <headersfsm> must be specified",required=False,type=str)
cmdargParser.add_argument("-I","--indent", required=False,default=0, help = "Indicates the number of spaces to indent each line",type=int)
cmdargParser.add_argument("-l","--eol",required=False,default="unix",help="Indicates the style of line ending to use, either \"windows\", \"unix\" or \"apple\"",type=str)
cmdargs = cmdargParser.parse_args()

res_c_runner = """const unsigned char* s = (const unsigned char*)path_and_query;
int state = 1, accept = -1, bol = 1;
for (;; s++) {
	int at_end = *s == '\\0' || *s == '?';
	if (bol && fsm_data[state + 1] != -1) state = fsm_data[state + 1];                         /* ^ */
	if ((at_end || *s == fsm_data[0]) && fsm_data[state + 2] != -1) state = fsm_data[state + 2];     /* $ */
	if (at_end) return fsm_data[state];
	int c = *s, next = -1;
	const TYPE* r = fsm_data + state + 4;
	for (int k = 0; k < fsm_data[state + 3] && c >= r[0]; k++, r += 3)
		if (c <= r[1]) { next = r[2]; break; }
	if (next == -1) break;
	state = next;
	bol = (c == fsm_data[0]);
}
return accept;
"""

def toSZLiteralBytes(data, startSpacing = 0):
    global eol
    length = len(data)
    result = "\""
    j = startSpacing
    i = 0
    while i<length:
        byte = data[i]
        c = chr(byte)
        if i > 0 and 0 == (j % 80) and i < len(data) - 1:
            result +=(f"\"{eol}    \"")
        if c == '\"':
            result += "\\\""
        elif c == '\\':
            result += "\\\\"
        elif c == '\r':
            result += "\\r"
        elif c == '\n':
            result += "\\n"
        elif c == '\t':
            result += "\\t"
        elif byte >= 32 and byte < 128:
            result += chr(byte)
        else:
            result += "\\x"
            result += format(byte, '02x')
        i = i + 1
        j = j + 1
    result += "\""
    return result

def toSZLiteral(string, startSpacing = 0):
    return toSZLiteralBytes(string.encode("utf-8"),startSpacing)

def emit(content):
    global output
    global indent
    global firstEmit
    global eol
    content = str(content)
    if firstEmit == True:
        firstEmit = False
        content = indent + content
    i = 0
    contentLen = len(content)
    xform = ""
    while i<contentLen:
        ch = content[i]
        if ch=='\r' and i < contentLen-1 and content[i+1]=='\n':
            xform += eol + indent
        elif ch=='\n':
            xform += eol + indent
        else:
            xform += ch
        i += 1
        
    content = xform
    if output is None:
        print(content, end="")
    else:
        output.write(content)

def emitLines(content):
    lines = content.splitlines()
    for line in lines:
        emit(line+eol)

def emitRawResponseBlock(input):
    global eol
    global cmdargs
    if len(input) > 0:
        data = input.encode("utf-8")
        emit(f"{cmdargs.block} ({toSZLiteralBytes(data,len(cmdargs.block))}, {len(data)}, {cmdargs.state});{eol}")

def emitDataFieldDecl(prologue, data):
    global eol
    length = len(data)
    emit("static const unsigned char http_response_data[] = {")
    prodata = prologue.encode("utf-8")
    prodatalen = len(prodata)
    i = 0
    while i < prodatalen:
        if (i % 20) == 0:
            emit(eol)
            if i < prodatalen + length - 1:
                emit("    ")
        entry = "0x" + format(prodata[i],'02x')
        if i < prodatalen + length - 1:
            entry += ", "
        emit(entry)
        i = i + 1       
    length += prodatalen
    j = 0
    while i < length:
        if (i % 20) == 0:
            emit(eol)
            if i < length - 1:
                emit("    ")
        entry = "0x" + format(data[j],'02x')
        if i < length - 1:
            entry += ", "
        emit(entry)
        i = i + 1
        j = j + 1
    if (i % 20) != 0:
        emit(" ")
    emit("};"+eol)

def pathUrlEncode(path):
    return quote(path,safe = "/")

def makeSafeName(relpath, names = None):
    if relpath is None or len(relpath) == 0:
        return relpath

    start = 0
    while start < len(relpath) and (relpath[start] == '/' or relpath[start] == '\\' or relpath[start] == '.'):
        start += 1
    
    if start == len(relpath):
        return ""
    
    sb = ""
    i = start
    while i < len(relpath):
        ch = relpath[i]
        if ch == '_' or (ch >= '0' and ch <= '9') or (ch >= 'A' and ch <= 'Z') or (ch >= 'a' and ch <= 'z'):
            sb += ch
        else:
            if len(sb) > 0 and sb[len(sb) - 1] != '_':
                sb += "_"
        i += 1

    result = sb
    if not (names is None):
        if result in names:
            i = 2
            while (result + str(i)) in names:
                i += 1
            result += str(i)
        names.append(result)

    return result

def fsmWidthBytes(table):
    width = 1
    i = 0
    while i < len(table):
        entry = table[i]
        if entry > 32767 or entry < -32768:
            return 4
        elif entry > 127 or entry < -128:
            width = 2
        i += 1
    return width

def fsmWidthToSignedType(width):
    return f"int{width * 8}_t"

def fsmReplaceTypes(s):
    s = s.replace("UINT8", "uint8_t")
    s = s.replace("INT8", "int8_t")
    s = s.replace("UINT16", "uint16_t")
    s = s.replace("INT16", "int16_t")
    s = s.replace("UINT32", "uint32_t")
    s = s.replace("INT32", "int32_t")
    return s

def emitFsm(handlers, maps):
    hfas = [None] * (len(handlers) + len(maps))
    i = 0
    while i < len(handlers):
        h = handlers[i]
        hfas[i] = fsm_escape(h[1])
        i += 1
    i = 0
    while i < len(maps):
        if maps[i][1] == True:
            hfas[i + len(handlers)] = fsm_escape(maps[i][0])
        else:
            hfas[i + len(handlers)] = maps[i][0]
        i += 1
    lexer = Builder.build(hfas, False)
    fsmData = compile_dfa(lexer)
    rsrc = res_c_runner
    width = fsmWidthBytes(fsmData)
    emit(f"static const {fsmWidthToSignedType(width)} fsm_data[] = {{")
    i = 0
    while i < len(fsmData):
        if (i % (40 / width)) == 0:
            emit(eol)
            if i < len(fsmData) - 1:
                emit("    ")
        entry = str(fsmData[i])
        if i < len(fsmData) - 1:
            entry += ", "
        emit(entry)
        i += 1

    emit(" };"+eol+eol)
    for line in rsrc.splitlines():
        match width:
            case 1:
                s = line.replace("TYPE","INT8")
            case 2:
                s = line.replace("TYPE","INT16")
            case _:
                s = line.replace("TYPE","INT32")
        s = fsmReplaceTypes(s)
        s = s.replace("RULE_COUNT",str(len(hfas)))
        emit(s+eol)

def run():
    global output
    global indent
    global eol
    if cmdargs.handlers == "none" and (not (cmdargs.index is None)):
        raise Exception("--handlers \"none\" cannot be specified with --index")

    if cmdargs.handlers == "none" and cmdargs.handlerfsm == True:
        raise Exception("--handlers \"none\" cannot be specified with --handlersfsm")
    
    if (not (cmdargs.urlmap is None)) and cmdargs.handlerfsm == False:
        raise Exception("--handlersfsm must be specified with --urlmap")
    
    if cmdargs.eol == "windows":
        eol = "\r\n"
    elif cmdargs.eol == "apple":
        eol = "\r"
    else:
        eol = "\n"

    if cmdargs.indent != None:
        indent = ""
        i = int(cmdargs.indent)
        while i > 0:
            indent += " "
            i -= 1
    if not (cmdargs.output is None):
        output = open(cmdargs.output,"w")
    if cmdargs.index is None:
        cmdargs.index = "index.*"
    if cmdargs.prefix is None:
        cmdargs.prefix = ""

    prolStr = ""
    if not cmdargs.prologue is None:
        stm = open(cmdargs.prologue,"rb")
        prolStr = stm.read().decode("utf-8-sig")
        stm.close()
    
    epilStr = ""
    if not cmdargs.epilogue is None:
        stm = open(cmdargs.epilogue,"rb")
        epilStr = stm.read().decode("utf-8-sig")
        stm.close()
                    
    allfiles = [os.path.join(dp, f) for dp, dn, filenames in os.walk(cmdargs.input) for f in filenames]
    indexes = [y for x in os.walk(cmdargs.input) for y in glob(os.path.join(x[0], cmdargs.index))]
    i = 0
    while i<len(allfiles):
        allfiles[i] = os.path.abspath(allfiles[i])
        i += 1
    i = 0
    while i<len(indexes):
        indexes[i] = os.path.abspath(indexes[i])
        i += 1

    usedNames = []
    
    files = dict()
    
    for f in allfiles:
        files[makeSafeName(os.path.relpath(f,os.path.abspath(cmdargs.input)).replace(os.path.sep, '/'),usedNames)]=f

    includes = ""
    includes += f"#include <stdint.h>{eol}#include <stddef.h>{eol}"

    oname = os.path.splitext(os.path.basename(cmdargs.output))[0]
    fname = os.path.split(os.path.abspath(cmdargs.input))[1]
    if not (cmdargs.output is None):
        output = open(cmdargs.output,"w")
        oname = os.path.splitext(os.path.basename(cmdargs.output))[0]
        fname = oname
    
    guardName = makeSafeName(fname.upper() + "_H",usedNames)
    emit(f"// Generated with clasptree{eol}")
    emit(f"// To use this file, define {fname.upper()}_IMPLEMENTATION in exactly one translation unit (.c/.cpp file) before including this header.{eol}")
    emit(f"#ifndef {guardName}{eol}")
    emit(f"#define {guardName}{eol}")
    emit(eol)
    emit(includes + eol)
    
    handlersList = []
    mapList = []

    if cmdargs.handlers!="none":
        if not (cmdargs.urlmap is None):
            urlmap = open(cmdargs.urlmap,"r")
            mapline = ""
            maplineno = 0
            mapline = urlmap.readline()
            while True:
                maplineno += 1
                idx = mapline.find('#')
                if idx > -1:
                    mapline = mapline[0:idx]
                
                mapline = mapline.rstrip()
                if len(mapline)==0: 
                    mapline = urlmap.readline()
                    if not mapline:
                        break
                    continue
                # find the split
                mappath = ""
                splitIndex = -1
                if mapline.startswith("\""):
                    i = 0
                    while i < len(mapline) - 1:
                        if line[i] == '"':
                            if line[i + 1] == '"':
                                mappath += "\""
                                # skip the next char
                                i += 1
                                continue
                            splitIndex = i + 1
                            break
                        else:
                            mappath += mapline[i]
                        i+=1
                    if splitIndex == -1:
                        raise Exception(f"No expression entry at line {maplineno}")
                else:
                    splitIndex = mapline.find(' ')
                    if splitIndex == -1:
                        raise Exception(f"No expression entry at line {maplineno}")
                    
                    mappath+= mapline[0:splitIndex]
                
                isLiteral = mapline.endswith('\"')
                expr = mapline[splitIndex + 1:]

                mapList.append((expr[1:-1], isLiteral, mappath))
                mapline = urlmap.readline()
                if not mapline:
                    break
            urlmap.close()
        for f in files.items():
            if os.path.basename(f[1]).startswith("."):
                continue
            
            mname = f[1][len(os.path.abspath(cmdargs.input))+1:].replace(os.path.sep,"/")
            if f[1] in indexes:
                # generate a default for this one
                dname = ""
                li = mname.rfind('/')
                if li > -1:
                    dname = mname[0: li + 1]
                
                hext = None
                hstd = f"/{dname}"
                if len(dname) > 1:
                    hext = "/" + dname[0:-1]
                sn = f"{cmdargs.prefix}content_{f[0]}"
                handlersList.append((hstd, pathUrlEncode(hstd), sn))
                if not (hext is None) and cmdargs.handlers == "extended":
                    handlersList.append((hext, pathUrlEncode(hext), sn))
                
            handlersList.append(("/" + mname, "/" + pathUrlEncode(mname), f"{cmdargs.prefix}content_{f[0]}"))
        
        handlersList.sort(key=lambda x: x[0])
 
        emit(f"#define {cmdargs.prefix.upper()}RESPONSE_HANDLER_COUNT {len(handlersList) + len(mapList)}{eol}")
 
        emit(f"#ifdef __cplusplus{eol}")
        emit("extern \"C\" {"+eol)
        emit(f"#endif{eol}")
        emit(eol)
        emit("typedef struct { const char* path; const char* path_encoded; void (* handler) (void* arg); } "+f"{cmdargs.prefix}response_handler_t;{eol}")
        emit(f"extern {cmdargs.prefix}response_handler_t {cmdargs.prefix}response_handlers[{cmdargs.prefix.upper()}RESPONSE_HANDLER_COUNT];{eol}")

        for f in files.items():
            p = os.path.relpath(f[1],os.path.abspath(cmdargs.input)).replace(os.path.sep, '/')
            emit(f"// .{p}{eol}")
            emit(f"void {cmdargs.prefix}content_{f[0]}(void* {cmdargs.state});{eol}")

        if cmdargs.handlerfsm == True:
            emit(f"/// @brief Matches a path to one of the response handler entries{eol}/// @param path_and_query The path to match which can include the query string (ignored){eol}/// @return The index of the response handler entry, or -1 if no match{eol}")
            emit(f"int {cmdargs.prefix}response_handler_match(const char* path_and_query);{eol}")

        emit(eol)

        emit(f"#ifdef __cplusplus{eol}")
        emit("}"+eol)
        emit(f"#endif{eol}{eol}")
        emit(f"#endif // {guardName}{eol}{eol}")
        impl = fname.upper() + "_IMPLEMENTATION"
        emit(f"#ifdef {impl}{eol}{eol}")
        if not (cmdargs.extra is None) and len(cmdargs.extra)>0:
            emit(f"#include \"{cmdargs.extra}\"{eol}{eol}")
        
        if cmdargs.handlers != "none":
            emit(f"{cmdargs.prefix}response_handler_t {cmdargs.prefix}response_handlers[{len(handlersList) + len(mapList)}] = {{{eol}")
            i = 0
            while  i < len(handlersList):
                handler = handlersList[i]
                emit("    { ")
                emit(f"{toSZLiteral(handler[0])}")
                emit(", ")
                emit(f"{toSZLiteral(handler[1])}, {handler[2]}")
                if i < len(handlersList) + len(mapList) - 1:
                    emit(" },"+eol)
                else:
                    emit(" }"+eol)
                i += 1
            i = 0
            while i < len(mapList):
                emit("    { ")
                if mapList[i][1]==False:
                    emit("\"\", \"\", ")
                else:

                    emit(toSZLiteral(mapList[i][0])+", "+toSZLiteral(mapList[i][2].replace(" ","%20"))+", ")
                mname = mapList[i][2]
                sn = makeSafeName(mname)
                emit(f"{cmdargs.prefix}content_{sn}")
                if i < len(mapList) - 1:
                    emit(" },"+eol)
                else:
                    emit(" }"+eol)
                i += 1
            emit("};"+eol)
        emit(eol)
        if cmdargs.handlerfsm == True:
            
            emit(f"// matches a path to a response handler index{eol}");
            emit(f"int {cmdargs.prefix}response_handler_match(const char* path_and_query) {{{eol}")
            oldindent = indent
            indent += "    "
            emit("    ") # hack to correct indent
            emitFsm(handlersList, mapList)
            indent = oldindent
            emit(eol) # hack to correct indent
            emit("}"+eol)
        
        for f in files.items():    
            mname = f[1][len(os.path.abspath(cmdargs.input))+1:].replace(os.path.sep, '/')
            exti = f[1].rfind(".")
            ext = ""
            if exti > -1:
                ext = f[1][exti:]
            emit(f"void {cmdargs.prefix}content_{f[0]}(void* {cmdargs.state}) {{{eol}")
            if ext.lower() == ".clasp":
                cmd = ["python","clasp.py"]
                cmd.append(f"{f[1]}")
                cmd.append(f"-l unix")
                cmd.append(f"-b {cmdargs.block}")
                cmd.append(f"-e {cmdargs.expr}")
                cmd.append(f"-s {cmdargs.state}")
                oldindent = indent
                indent += "    "
                if len(prolStr) > 0:
                    emit ("    ")
                    emit(f"{prolStr}{eol}")
                result = subprocess.run(cmd,stdout=subprocess.PIPE)
                emitLines("    "+result.stdout.decode('utf-8'))
                
                if len(epilStr) > 0:
                    emit ("   ")
                    emit(f"{epilStr}{eol}")
                    indent = oldindent
                    emit(eol)
                else:
                    indent = oldindent
            else:
                cmd = ["python","clstat.py"]
                cmd.append(f"{f[1]}")
                cmd.append("-S 200")
                cmd.append("-T OK")
                cmd.append(f"-l unix")
                cmd.append(f"-b {cmdargs.block}")
                cmd.append(f"-s {cmdargs.state}")
                if cmdargs.nostatus == True:
                    cmd.append("-n")
                oldindent = indent
                indent += "    "
                if len(prolStr) > 0:
                    emit ("    ")
                    emit(f"{prolStr}{eol}")
                result = subprocess.run(cmd,stdout=subprocess.PIPE)
                emitLines("    "+result.stdout.decode('utf-8'))
                
                    
                if len(epilStr) > 0:
                    emit ("   ")
                    emit(f"{epilStr}{eol}")
                    indent = oldindent
                    emit(eol)
                else:
                    indent = oldindent
            emit(eol)  
            emit("}"+eol)
        emit(f"#endif // {impl}\r\n")
    if not (output is None):
        output.close()

run()