#include <iostream>
#include <fstream>
#include <string>
#include <vector>
#include <regex>
#include <numeric>
#include <windows.h>
#include <unordered_map>
#include <set>
#include <algorithm>
#include <map>

std::set<std::wstring> csharpKeywords = {
    L"abstract", L"as", L"base", L"bool", L"break", L"byte", L"case", L"catch", L"char", L"checked", L"class", L"const", L"continue", L"decimal", L"default", L"delegate", L"do", L"double", L"else", L"enum", L"event", L"explicit", L"extern", L"false", L"finally", L"fixed", L"float", L"for", L"foreach", L"goto", L"if", L"implicit", L"in", L"int", L"interface", L"internal", L"is", L"lock", L"long", L"namespace", L"new", L"null", L"object", L"operator", L"out", L"override", L"params", L"private", L"protected", L"public", L"readonly", L"ref", L"return", L"sbyte", L"sealed", L"short", L"sizeof", L"stackalloc", L"static", L"string", L"struct", L"switch", L"this", L"throw", L"true", L"try", L"typeof", L"uint", L"ulong", L"unchecked", L"unsafe", L"ushort", L"using", L"virtual", L"void", L"volatile", L"while"
};

std::wofstream logFile;

void log(const std::wstring& msg) {
    logFile << msg << L"\n";
    logFile.flush();
    std::wcout << msg << L"\n";
}

std::wstring readFile(const std::wstring& path) {
    log(std::wstring(L"Reading file: ") + path);
    std::wifstream file(path);
    if (!file.is_open()) {
        log(std::wstring(L"Failed to open file: ") + path);
        return L"";
    }
    try {
        std::wstring content((std::istreambuf_iterator<wchar_t>(file)), std::istreambuf_iterator<wchar_t>());
        file.close();
        log(std::wstring(L"Read ") + std::to_wstring(content.length()) + std::wstring(L" chars from: ") + path);
        return content;
    }
    catch (const std::exception& e) {
        std::string eStr = e.what();
        std::wstring wE(eStr.begin(), eStr.end());
        log(std::wstring(L"Exception reading ") + path + std::wstring(L": ") + wE);
        file.close();
        return L"";
    }
}

std::wstring trim(const std::wstring& str) {
    size_t first = str.find_first_not_of(L" \t");
    if (first == std::wstring::npos) return L"";
    size_t last = str.find_last_not_of(L" \t");
    return str.substr(first, last - first + 1);
}

std::vector<std::wstring> better_split(const std::wstring& str, wchar_t delim) {
    std::vector<std::wstring> res;
    size_t start = 0;
    int angleCount = 0;
    for (size_t i = 0; i < str.length(); ++i) {
        wchar_t c = str[i];
        if (c == L'<') angleCount++;
        else if (c == L'>') angleCount--;
        else if (c == delim && angleCount == 0) {
            res.push_back(trim(str.substr(start, i - start)));
            start = i + 1;
        }
    }
    res.push_back(trim(str.substr(start)));
    return res;
}

std::vector<std::wstring> split_args(const std::wstring& str) {
    std::vector<std::wstring> res;
    size_t start = 0;
    int paren = 0, brace = 0, brack = 0, angle = 0;
    for (size_t i = 0; i < str.length(); ++i) {
        wchar_t c = str[i];
        if (c == L'(') paren++;
        else if (c == L')') paren--;
        else if (c == L'{') brace++;
        else if (c == L'}') brace--;
        else if (c == L'[') brack++;
        else if (c == L']') brack--;
        else if (c == L'<') angle++;
        else if (c == L'>') angle--;
        else if (c == L',' && paren == 0 && brace == 0 && brack == 0 && angle == 0) {
            res.push_back(trim(str.substr(start, i - start)));
            start = i + 1;
        }
    }
    if (start < str.length()) res.push_back(trim(str.substr(start)));
    return res;
}

struct MethodInfo {
    std::wstring returnType;
    std::wstring name;
    std::vector<std::wstring> args;
    std::set<std::wstring> calls;
    std::wstring body;
};

struct FieldInfo {
    std::wstring type;
    std::wstring name;
};

struct ClassInfo {
    std::wstring type;
    std::wstring name;
    std::wstring ns;
    std::wstring fileName;
    std::wstring folder;
    std::wstring baseClasses;
    std::vector<std::wstring> bases;
    std::vector<std::wstring> uses;
    std::vector<FieldInfo> fields;
    std::vector<MethodInfo> methods;
};

std::unordered_map<std::wstring, std::set<std::wstring>> methodNames;

std::wstring extractBody(const std::wstring& s, size_t startPos) {
    size_t bracePos = s.find(L"{", startPos);
    if (bracePos == std::wstring::npos) return L"";
    int braceCount = 1;
    size_t pos = bracePos + 1;
    bool inString = false;
    bool inVerbatim = false;
    wchar_t quoteChar = 0;
    bool escape = false;
    bool inLineComment = false;
    bool inMultiComment = false;
    size_t bodyStart = pos;
    while (pos < s.length() && braceCount > 0) {
        wchar_t c = s[pos];
        if (inMultiComment) {
            if (c == L'*' && pos + 1 < s.length() && s[pos + 1] == L'/') {
                inMultiComment = false;
                pos++;
            }
            pos++;
            continue;
        }
        if (inLineComment) {
            if (c == L'\n') inLineComment = false;
            pos++;
            continue;
        }
        if (inString) {
            if (inVerbatim) {
                if (c == L'"') {
                    if (pos + 1 < s.length() && s[pos + 1] == L'"') {
                        pos++;
                    }
                    else {
                        inString = false;
                        inVerbatim = false;
                    }
                }
            }
            else {
                if (c == L'\\') {
                    escape = true;
                }
                else if (c == quoteChar && !escape) {
                    inString = false;
                }
                escape = false;
            }
            pos++;
            continue;
        }
        if (c == L'/' && pos + 1 < s.length()) {
            if (s[pos + 1] == L'/') {
                inLineComment = true;
                pos += 2;
                continue;
            }
            else if (s[pos + 1] == L'*') {
                inMultiComment = true;
                pos += 2;
                continue;
            }
        }
        if ((c == L'"' || c == L'\'') && !inLineComment && !inMultiComment) {
            inString = true;
            quoteChar = c;
            inVerbatim = (c == L'"') && (pos > bracePos) && (s[pos - 1] == L'@');
            pos++;
            continue;
        }
        if (c == L'{') braceCount++;
        else if (c == L'}') braceCount--;
        pos++;
    }
    if (braceCount == 0) return s.substr(bodyStart, pos - bodyStart - 1);
    return L"";
}

std::vector<std::pair<size_t, size_t>> parseClasses(const std::wstring& body, size_t bodyOffset, const std::wstring& parent, std::vector<ClassInfo>& classes, const std::wstring& folder, const std::wstring& fileName, const std::vector<std::wstring>& uses) {
    std::wregex classRegex(LR"((?:(?:public|private|internal|protected|sealed|abstract|static)\s+)*\s*(class|interface|struct|enum)\s+(\w+)(?:\s*:\s*([\w<>\s,]+))?)");
    std::wsregex_iterator classIt(body.begin(), body.end(), classRegex);
    std::vector<std::pair<size_t, size_t>> thisLevelRanges;
    for (; classIt != std::wsregex_iterator(); ++classIt) {
        size_t relDeclStart = classIt->position();
        size_t relDeclEnd = relDeclStart + classIt->length();
        size_t relBracePos = body.find(L"{", relDeclEnd);
        if (relBracePos == std::wstring::npos) continue;
        std::wstring classInnerBody = extractBody(body, relDeclEnd);
        if (classInnerBody.empty()) continue;
        size_t relInnerStart = relBracePos + 1;
        size_t relInnerEnd = relInnerStart + classInnerBody.length();
        size_t relClassEnd = relBracePos + 1 + classInnerBody.length() + 1;
        std::wstring fullParent = parent.empty() ? classIt->str(2) : parent + L"." + classIt->str(2);
        std::vector<std::pair<size_t, size_t>> nestedInnerRanges = parseClasses(classInnerBody, bodyOffset + relInnerStart, fullParent, classes, folder, fileName, uses);
        ClassInfo ci;
        ci.type = classIt->str(1);
        ci.name = classIt->str(2);
        ci.ns = parent;
        ci.fileName = fileName;
        ci.folder = folder;
        ci.baseClasses = classIt->str(3).empty() ? L"None" : classIt->str(3);
        ci.bases = better_split(ci.baseClasses, L',');
        ci.uses = uses;
        std::vector<std::pair<size_t, size_t>> methodBodies;
        std::wregex methodRegex(LR"((?:(?:public|private|protected|internal|static|async|virtual|override|abstract|sealed|extern|new)\s+)*(?:([\w<>\[\]]+)\s+)?(\w+)\s*\(([^)]*)\)\s*(?:\{|;))");
        std::wsregex_iterator methodIt(classInnerBody.begin(), classInnerBody.end(), methodRegex);
        for (; methodIt != std::wsregex_iterator(); ++methodIt) {
            size_t methodRelPos = methodIt->position();
            bool insideNested = false;
            for (const auto& nr : nestedInnerRanges) {
                if (methodRelPos >= nr.first && methodRelPos < nr.second) {
                    insideNested = true;
                    break;
                }
            }
            if (insideNested) continue;
            MethodInfo mi;
            mi.name = methodIt->str(2);
            if (csharpKeywords.count(mi.name) > 0) continue;
            if (methodIt->length(1) > 0) {
                mi.returnType = methodIt->str(1);
            }
            else {
                if (mi.name == ci.name) {
                    mi.returnType = L"constructor";
                }
                else {
                    continue;
                }
            }
            std::wstring argsStr = methodIt->str(3);
            mi.args = split_args(argsStr);
            std::wstring fullClass = ci.ns.empty() ? ci.name : ci.ns + L"." + ci.name;
            methodNames[mi.name].insert(fullClass);
            mi.body = extractBody(classInnerBody, methodRelPos);
            mi.body = std::regex_replace(mi.body, std::wregex(LR"(\s+)"), L" ");
            if (mi.body.empty() || std::regex_match(mi.body, std::wregex(LR"(\s*(get|set);\s*)"))) continue;
            size_t declLen = methodIt->length();
            std::wstring endChar = classInnerBody.substr(methodRelPos + declLen - 1, 1);
            if (endChar == L"{") {
                size_t bodyStart = methodRelPos + declLen - 1;
                size_t bodyEnd = bodyStart + mi.body.length() + 2;
                methodBodies.push_back({ bodyStart, bodyEnd });
            }
            ci.methods.push_back(mi);
        }
        std::wregex fieldRegex(LR"((?:(?:public|private|protected|internal|static|readonly|const)\s+)*([\w<>\[\]]+)\s+(\w+)(?:\s*=\s*[^;]+)?;)");
        std::wsregex_iterator fieldIt(classInnerBody.begin(), classInnerBody.end(), fieldRegex);
        for (; fieldIt != std::wsregex_iterator(); ++fieldIt) {
            size_t fieldRelPos = fieldIt->position();
            bool insideNested = false;
            for (const auto& nr : nestedInnerRanges) {
                if (fieldRelPos >= nr.first && fieldRelPos < nr.second) {
                    insideNested = true;
                    break;
                }
            }
            if (insideNested) continue;
            bool insideMethod = false;
            for (const auto& mb : methodBodies) {
                if (fieldRelPos >= mb.first && fieldRelPos < mb.second) {
                    insideMethod = true;
                    break;
                }
            }
            if (insideMethod) continue;
            FieldInfo fi;
            fi.type = fieldIt->str(1);
            fi.name = fieldIt->str(2);
            ci.fields.push_back(fi);
        }
        classes.push_back(ci);
        thisLevelRanges.push_back({ relDeclStart, relClassEnd });
        for (const auto& nr : nestedInnerRanges) {
            size_t adjStart = relInnerStart + nr.first;
            size_t adjEnd = relInnerStart + nr.second;
            thisLevelRanges.push_back({ adjStart, adjEnd });
        }
    }
    return thisLevelRanges;
}

void parseFile(const std::wstring& path, std::map<std::wstring, std::map<std::wstring, std::vector<ClassInfo>>>& folderFileClasses, const std::wstring& repoPath) {
    log(std::wstring(L"Parsing file: ") + path);
    std::wstring content = readFile(path);
    if (content.empty()) {
        log(std::wstring(L"Skipping empty file: ") + path);
        return;
    }
    std::wstring folder;
    try {
        size_t pos = path.find(repoPath);
        if (pos == std::wstring::npos) {
            log(std::wstring(L"Path does not contain repo path: ") + path);
            return;
        }
        folder = path.substr(pos + repoPath.length() + 1); // +1 for the backslash
        size_t slashPos = folder.find_first_of(L"\\");
        if (slashPos != std::wstring::npos) {
            folder = folder.substr(0, slashPos);
        }
        else {
            folder = L"Root";
        }
    }
    catch (const std::exception& e) {
        std::string eStr = e.what();
        std::wstring wE(eStr.begin(), eStr.end());
        log(std::wstring(L"Exception getting folder from ") + path + std::wstring(L": ") + wE);
        return;
    }
    std::wstring fileName = path.substr(path.rfind(L"\\") + 1);
    std::wstring ns;
    try {
        std::wregex nsRegex(LR"(namespace\s+([\w\.]+))");
        std::wsmatch nsMatch;
        ns = std::regex_search(content, nsMatch, nsRegex) ? nsMatch[1].str() : L"None";
        log(std::wstring(L"Namespace: ") + ns);
    }
    catch (const std::exception& e) {
        std::string eStr = e.what();
        std::wstring wE(eStr.begin(), eStr.end());
        log(std::wstring(L"Exception parsing namespace in ") + path + std::wstring(L": ") + wE);
        ns = L"None";
    }
    std::vector<std::wstring> uses;
    try {
        std::wregex useRegex(LR"(using\s+([\w\.]+);)");
        std::wsregex_iterator useIt(content.begin(), content.end(), useRegex);
        std::set<std::wstring> uniqueUses;
        for (; useIt != std::wsregex_iterator(); ++useIt) {
            uniqueUses.insert(useIt->str(1));
        }
        uses.assign(uniqueUses.begin(), uniqueUses.end());
        log(std::wstring(L"Found ") + std::to_wstring(uses.size()) + std::wstring(L" dependencies"));
    }
    catch (const std::exception& e) {
        std::string eStr = e.what();
        std::wstring wE(eStr.begin(), eStr.end());
        log(std::wstring(L"Exception parsing uses in ") + path + std::wstring(L": ") + wE);
    }
    try {
        std::vector<ClassInfo> classes;
        parseClasses(content, 0, ns, classes, folder, fileName, uses);
        if (!classes.empty()) {
            folderFileClasses[folder][fileName] = std::move(classes);
        }
    }
    catch (const std::exception& e) {
        std::string eStr = e.what();
        std::wstring wE(eStr.begin(), eStr.end());
        log(std::wstring(L"Exception parsing classes in ") + path + std::wstring(L": ") + wE);
    }
}

void analyzeMethodCalls(std::map<std::wstring, std::map<std::wstring, std::vector<ClassInfo>>>& folderFileClasses) {
    for (auto& folderPair : folderFileClasses) {
        for (auto& filePair : folderPair.second) {
            for (auto& ci : filePair.second) {
                for (MethodInfo& mi : ci.methods) {
                    std::wregex callRegex(LR"((?:([\w\.]+)\.)?(\w+)\s*\()");
                    std::wsregex_iterator callIt(mi.body.begin(), mi.body.end(), callRegex);
                    for (; callIt != std::wsregex_iterator(); ++callIt) {
                        std::wstring prefix = callIt->str(1);
                        std::wstring methodName = callIt->str(2);
                        if (methodNames.count(methodName) && methodName != mi.name) {
                            std::wstring qualified = prefix.empty() ? methodName : prefix + L"." + methodName;
                            mi.calls.insert(qualified);
                        }
                    }
                }
            }
        }
    }
}

void listFiles(const std::wstring& folderPath, std::map<std::wstring, std::map<std::wstring, std::vector<ClassInfo>>>& folderFileClasses, const std::wstring& repoPath, int depth = 0) {
    if (depth > 100) {
        log(std::wstring(L"Maximum recursion depth reached at: ") + folderPath);
        return;
    }
    log(std::wstring(L"Listing folder: ") + folderPath);
    WIN32_FIND_DATAW findData;
    HANDLE hFind;
    std::wstring searchPath = folderPath + L"\\*";
    hFind = FindFirstFileW(searchPath.c_str(), &findData);
    if (hFind == INVALID_HANDLE_VALUE) {
        log(std::wstring(L"Failed to scan folder: ") + folderPath + std::wstring(L" (Error: ") + std::to_wstring(GetLastError()) + L")");
        return;
    }
    int iterationCount = 0;
    const int maxIterations = 10000;
    BOOL result = TRUE;
    while (result) {
        if (++iterationCount > maxIterations) {
            log(std::wstring(L"Too many files in ") + folderPath + std::wstring(L", skipping"));
            break;
        }
        std::wstring name = findData.cFileName;
        log(std::wstring(L"Found: ") + name);
        if (name != L"." && name != L".." && name[0] != L'.') {
            std::wstring fullPath = folderPath + L"\\" + name;
            if (findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
                bool ignore = false;
                if (name == L"bin" || name == L"obj" || name == L".vs" || name == L".git" || name == L"packages") {
                    log(std::wstring(L"Ignoring non-source folder: ") + fullPath);
                    ignore = true;
                }
                else if (findData.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) {
                    log(std::wstring(L"Skipping junction or symbolic link: ") + fullPath);
                    ignore = true;
                }
                if (!ignore) {
                    listFiles(fullPath, folderFileClasses, repoPath, depth + 1);
                }
            }
            else if (name.length() > 3 && name.substr(name.length() - 3) == L".cs") {
                parseFile(fullPath, folderFileClasses, repoPath);
            }
        }
        result = FindNextFileW(hFind, &findData);
    }
    DWORD error = GetLastError();
    if (error != ERROR_NO_MORE_FILES) {
        log(std::wstring(L"Error listing files in ") + folderPath + std::wstring(L" (Error: ") + std::to_wstring(error) + L")");
    }
    FindClose(hFind);
}

void outputHierarchy(std::wofstream& output, const std::wstring& cls, int level, const std::unordered_map<std::wstring, std::vector<std::wstring>>& inheritance) {
    for (int i = 0; i < level; i++) output << L" ";
    output << cls << L"\n";
    auto it = inheritance.find(cls);
    if (it != inheritance.end()) {
        std::vector<std::wstring> childs = it->second;
        std::sort(childs.begin(), childs.end());
        for (const auto& child : childs) {
            outputHierarchy(output, child, level + 1, inheritance);
        }
    }
}

int main() {
    logFile.open(L"summarize_log.txt");
    if (!logFile.is_open()) {
        std::wcout << L"Failed to create log file\n";
        system("pause");
        return 1;
    }
    log(L"Log file opened successfully");
    WCHAR currentPath[MAX_PATH];
    log(L"Getting current directory");
    if (!GetCurrentDirectoryW(MAX_PATH, currentPath)) {
        log(std::wstring(L"Failed to get current directory (Error: ") + std::to_wstring(GetLastError()) + L")");
        logFile.close();
        system("pause");
        return 1;
    }
    std::wstring repoPath = currentPath;
    log(std::wstring(L"Repo path set to: ") + repoPath);
    std::wofstream output(L"summary.txt");
    if (!output.is_open()) {
        log(L"Failed to create summary.txt");
        logFile.close();
        system("pause");
        return 1;
    }
    log(L"Summary file opened successfully");
    std::wcout << L"Scanning " << repoPath << L"...\n";
    log(L"Starting scan");
    std::map<std::wstring, std::map<std::wstring, std::vector<ClassInfo>>> folderFileClasses;
    listFiles(repoPath, folderFileClasses, repoPath, 0);
    analyzeMethodCalls(folderFileClasses);
    log(std::wstring(L"Writing output for ") + std::to_wstring(folderFileClasses.size()) + std::wstring(L" folders"));
    for (const auto& folderPair : folderFileClasses) {
        std::wstring folder = folderPair.first;
        output << L"Folder: " << folder << L"\n";
        for (const auto& filePair : folderPair.second) {
            std::wstring fileName = filePair.first;
            for (const auto& ci : filePair.second) {
                output << L"- File: " << fileName << L"\n";
                output << L" - Class: " << ci.name << L" (Namespace: " << ci.ns << L")\n";
                if (ci.baseClasses != L"None") {
                    output << L" - Inherits: " << ci.baseClasses << L"\n";
                }
                if (!ci.uses.empty()) {
                    output << L" - Dependencies: " << std::accumulate(ci.uses.begin(), ci.uses.end(), std::wstring(), [](const std::wstring& a, const std::wstring& b) { return a.empty() ? b : a + L", " + b; }) << L"\n";
                }
                if (!ci.fields.empty()) {
                    output << L" - Fields:\n";
                    for (const FieldInfo& fi : ci.fields) {
                        output << L" - " << fi.type << L" " << fi.name << L"\n";
                    }
                }
                if (!ci.methods.empty()) {
                    output << L" - Methods:\n";
                    for (const MethodInfo& mi : ci.methods) {
                        output << L" - " << mi.returnType << L" " << mi.name << L"(" << (mi.args.empty() ? L"" : std::accumulate(mi.args.begin(), mi.args.end(), std::wstring(), [](const std::wstring& a, const std::wstring& b) { return a.empty() ? b : a + L", " + b; })) << L")\n";
                        if (!mi.calls.empty()) {
                            std::vector<std::wstring> sortedCalls(mi.calls.begin(), mi.calls.end());
                            std::sort(sortedCalls.begin(), sortedCalls.end());
                            output << L" - Calls: " << std::accumulate(sortedCalls.begin(), sortedCalls.end(), std::wstring(), [](const std::wstring& a, const std::wstring& b) { return a.empty() ? b : a + L", " + b; }) << L"\n";
                        }
                    }
                }
            }
        }
        output << L"\n";
    }
    std::unordered_map<std::wstring, ClassInfo> allClassMap;
    for (const auto& folderPair : folderFileClasses) {
        for (const auto& filePair : folderPair.second) {
            for (const auto& ci : filePair.second) {
                std::wstring fullName = ci.ns.empty() ? ci.name : ci.ns + L"." + ci.name;
                allClassMap[fullName] = ci;
            }
        }
    }
    std::unordered_map<std::wstring, std::vector<std::wstring>> inheritance;
    for (const auto& p : allClassMap) {
        const ClassInfo& ci = p.second;
        std::vector<std::wstring> resolvedBases;
        for (std::wstring b : ci.bases) {
            b = trim(b);
            if (b.empty()) continue;
            if (b.find(L'.') == std::wstring::npos && ci.ns != L"None") {
                b = ci.ns + L"." + b;
            }
            resolvedBases.push_back(b);
        }
        for (const std::wstring& b : resolvedBases) {
            if (allClassMap.count(b)) {
                inheritance[b].push_back(p.first);
            }
        }
    }
    std::vector<std::wstring> roots;
    std::set<std::wstring> allClasses;
    for (const auto& p : allClassMap) {
        allClasses.insert(p.first);
    }
    for (const auto& p : allClassMap) {
        bool isRoot = true;
        for (const std::wstring& b : p.second.bases) {
            std::wstring rb = trim(b);
            if (rb.empty()) continue;
            if (rb.find(L'.') == std::wstring::npos && p.second.ns != L"None") {
                rb = p.second.ns + L"." + rb;
            }
            if (allClasses.count(rb)) {
                isRoot = false;
                break;
            }
        }
        if (isRoot) {
            roots.push_back(p.first);
        }
    }
    std::sort(roots.begin(), roots.end());
    output << L"Class Hierarchy:\n";
    for (const auto& root : roots) {
        outputHierarchy(output, root, 0, inheritance);
    }
    output.close();
    log(L"Scan complete");
    logFile.close();
    std::wcout << L"Done! Check summary.txt in " << repoPath << L"\n";
    system("pause");
    return 0;
}