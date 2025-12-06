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
    log(L"Reading file: " + path);
    std::wifstream file(path);
    if (!file.is_open()) {
        log(L"Failed to open file: " + path);
        return L"";
    }
    try {
        std::wstring content((std::istreambuf_iterator<wchar_t>(file)), std::istreambuf_iterator<wchar_t>());
        file.close();
        log(L"Read " + std::to_wstring(content.length()) + L" chars from: " + path);
        return content;
    }
    catch (const std::exception& e) {
        std::string eStr = e.what();
        std::wstring wE(eStr.begin(), eStr.end());
        log(L"Exception reading " + path + L": " + wE);
        file.close();
        return L"";
    }
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
    std::wstring baseClasses;
    std::vector<std::wstring> uses;
    std::vector<FieldInfo> fields;
    std::vector<MethodInfo> methods;
};

std::unordered_map<std::wstring, std::set<std::wstring>> methodNames;

std::wstring extractMethodBody(const std::wstring& content, size_t startPos) {
    size_t pos = content.find(L"{", startPos);
    if (pos == std::wstring::npos) return L"";
    int braceCount = 1;
    pos++;
    size_t bodyStart = pos;
    while (pos < content.length() && braceCount > 0) {
        if (content[pos] == L'{') braceCount++;
        else if (content[pos] == L'}') braceCount--;
        pos++;
    }
    if (braceCount == 0) return content.substr(bodyStart, pos - bodyStart - 1);
    return L"";
}

void parseFile(const std::wstring& path, std::unordered_map<std::wstring, std::vector<ClassInfo>>& folderClasses, const std::wstring& repoPath) {
    log(L"Parsing file: " + path);
    std::wstring content = readFile(path);
    if (content.empty()) {
        log(L"Skipping empty file: " + path);
        return;
    }
    std::wstring folder;
    try {
        size_t pos = path.find(repoPath);
        if (pos == std::wstring::npos) {
            log(L"Path does not contain repo path: " + path);
            return;
        }
        folder = path.substr(pos + repoPath.length() + 1); // +1 for the backslash
        size_t slashPos = folder.find_first_of(L"\\");
        if (slashPos != std::wstring::npos) {
            folder = folder.substr(0, slashPos);
        }
        else {
            folder = L"";
        }
    }
    catch (const std::exception& e) {
        std::string eStr = e.what();
        std::wstring wE(eStr.begin(), eStr.end());
        log(L"Exception getting folder from " + path + L": " + wE);
        return;
    }
    std::wstring fileName = path.substr(path.rfind(L"\\") + 1);
    std::wstring ns;
    try {
        std::wregex nsRegex(LR"(namespace\s+([\w\.]+))");
        std::wsmatch nsMatch;
        ns = std::regex_search(content, nsMatch, nsRegex) ? nsMatch[1].str() : L"None";
        log(L"Namespace: " + ns);
    }
    catch (const std::exception& e) {
        std::string eStr = e.what();
        std::wstring wE(eStr.begin(), eStr.end());
        log(L"Exception parsing namespace in " + path + L": " + wE);
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
        log(L"Found " + std::to_wstring(uses.size()) + L" dependencies");
    }
    catch (const std::exception& e) {
        std::string eStr = e.what();
        std::wstring wE(eStr.begin(), eStr.end());
        log(L"Exception parsing uses in " + path + L": " + wE);
    }
    std::unordered_map<std::wstring, ClassInfo> classMap;
    try {
        std::wregex classRegex(LR"((?:public|private|internal)?\s*(class|interface|enum)\s+(\w+)(?:\s*:\s*([\w,\s]+))?)");
        std::wsregex_iterator classIt(content.begin(), content.end(), classRegex);
        int classCount = 0;
        for (; classIt != std::wsregex_iterator(); ++classIt) {
            ClassInfo ci;
            ci.type = classIt->str(1);
            ci.name = classIt->str(2);
            ci.ns = ns;
            ci.fileName = fileName;
            ci.baseClasses = classIt->str(3).empty() ? L"None" : classIt->str(3);
            ci.uses = uses;
            size_t declEnd = classIt->position() + classIt->length();
            size_t bracePos = content.find(L"{", declEnd);
            if (bracePos == std::wstring::npos) {
                log(L"No opening brace for class " + ci.name + L" in " + path);
                continue;
            }
            int braceCount = 1;
            size_t pos = bracePos + 1;
            while (pos < content.length() && braceCount > 0) {
                if (content[pos] == L'{') braceCount++;
                else if (content[pos] == L'}') braceCount--;
                pos++;
            }
            if (braceCount != 0) {
                log(L"Unmatched braces for class " + ci.name + L" in " + path);
                continue;
            }
            std::wstring classBody = content.substr(bracePos + 1, pos - bracePos - 2);
            // Parse fields
            std::wregex fieldRegex(LR"((?:(?:public|private|protected|internal|static|readonly|const)\s+)*([\w<>\[\]]+)\s+(\w+)(?:\s*=\s*[^;]+)?;)");
            std::wsregex_iterator fieldIt(classBody.begin(), classBody.end(), fieldRegex);
            for (; fieldIt != std::wsregex_iterator(); ++fieldIt) {
                FieldInfo fi;
                fi.type = fieldIt->str(1);
                fi.name = fieldIt->str(2);
                ci.fields.push_back(fi);
            }
            std::wregex methodRegex(LR"((?:(?:public|private|protected|internal|static|async|virtual|override|abstract|sealed|extern|new)\s+)*(?:([\w<>\[\]]+)\s+)?(\w+)\s*\(([^)]*)\)\s*(?:\{|;))");
            std::wsregex_iterator methodIt(classBody.begin(), classBody.end(), methodRegex);
            int methodCount = 0;
            for (; methodIt != std::wsregex_iterator(); ++methodIt) {
                MethodInfo mi;
                mi.name = methodIt->str(2);
                if (csharpKeywords.count(mi.name) > 0) {
                    continue;
                }
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
                std::wregex argRegex(LR"((?:out|ref|params)?\s*[\w<>\[\]]+\s+\w+(?:\s*=\s*[^,]+)?)");
                std::wsregex_iterator argIt(argsStr.begin(), argsStr.end(), argRegex);
                std::set<std::wstring> uniqueArgs;
                for (; argIt != std::wsregex_iterator(); ++argIt) {
                    uniqueArgs.insert(argIt->str(0));
                }
                mi.args.assign(uniqueArgs.begin(), uniqueArgs.end());
                methodNames[mi.name].insert(ci.name);
                size_t methodStart = methodIt->position();
                mi.body = extractMethodBody(classBody, methodStart);
                mi.body = std::regex_replace(mi.body, std::wregex(LR"(\s+)"), L" ");
                if (mi.body.empty() || std::regex_match(mi.body, std::wregex(LR"(\s*(get|set);\s*)"))) {
                    continue; // Skip trivial methods
                }
                ci.methods.push_back(mi);
                methodCount++;
            }
            log(L"Class " + ci.name + L": " + std::to_wstring(methodCount) + L" methods");
            auto& existing = classMap[ci.name];
            if (existing.name.empty()) {
                existing = ci;
            }
            else {
                existing.methods.insert(existing.methods.end(), ci.methods.begin(), ci.methods.end());
                existing.fields.insert(existing.fields.end(), ci.fields.begin(), ci.fields.end());
            }
            classCount++;
        }
        for (auto& p : classMap) {
            folderClasses[folder].push_back(p.second);
        }
        log(L"Found " + std::to_wstring(classCount) + L" classes in " + path);
    }
    catch (const std::exception& e) {
        std::string eStr = e.what();
        std::wstring wE(eStr.begin(), eStr.end());
        log(L"Exception parsing classes in " + path + L": " + wE);
    }
}

void analyzeMethodCalls(std::unordered_map<std::wstring, std::vector<ClassInfo>>& folderClasses) {
    for (auto& pair : folderClasses) {
        for (ClassInfo& ci : pair.second) {
            for (MethodInfo& mi : ci.methods) {
                std::wregex callRegex(LR"((?:([\w\.]+)\.)?(\w+)\()");
                std::wsregex_iterator callIt(mi.body.begin(), mi.body.end(), callRegex);
                for (; callIt != std::wsregex_iterator(); ++callIt) {
                    std::wstring prefix = callIt->str(1);
                    std::wstring methodName = callIt->str(2);
                    if (methodNames.count(methodName) && methodName != mi.name) {  // Skip self-calls if same name
                        std::wstring qualified = prefix.empty() ? methodName : prefix + L"." + methodName;
                        mi.calls.insert(qualified);
                    }
                }
            }
        }
    }
}

void listFiles(const std::wstring& folderPath, std::unordered_map<std::wstring, std::vector<ClassInfo>>& folderClasses, const std::wstring& repoPath, int depth = 0) {
    if (depth > 100) {
        log(L"Maximum recursion depth reached at: " + folderPath);
        return;
    }
    log(L"Listing folder: " + folderPath);
    WIN32_FIND_DATAW findData;
    HANDLE hFind;
    std::wstring searchPath = folderPath + L"\\*";
    hFind = FindFirstFileW(searchPath.c_str(), &findData);
    if (hFind == INVALID_HANDLE_VALUE) {
        log(L"Failed to scan folder: " + folderPath + L" (Error: " + std::to_wstring(GetLastError()) + L")");
        return;
    }
    int iterationCount = 0;
    const int maxIterations = 10000;
    BOOL result = TRUE;
    while (result) {
        if (++iterationCount > maxIterations) {
            log(L"Too many files in " + folderPath + L", skipping");
            break;
        }
        std::wstring name = findData.cFileName;
        log(L"Found: " + name);
        if (name != L"." && name != L".." && name[0] != L'.') {
            std::wstring fullPath = folderPath + L"\\" + name;
            if (findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
                bool ignore = false;
                if (name == L"bin" || name == L"obj" || name == L".vs" || name == L".git" || name == L"packages") {
                    log(L"Ignoring non-source folder: " + fullPath);
                    ignore = true;
                }
                else if (findData.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) {
                    log(L"Skipping junction or symbolic link: " + fullPath);
                    ignore = true;
                }
                if (!ignore) {
                    listFiles(fullPath, folderClasses, repoPath, depth + 1);
                }
            }
            else if (name.length() > 3 && name.substr(name.length() - 3) == L".cs") {
                parseFile(fullPath, folderClasses, repoPath);
            }
        }
        result = FindNextFileW(hFind, &findData);
    }
    DWORD error = GetLastError();
    if (error != ERROR_NO_MORE_FILES) {
        log(L"Error listing files in " + folderPath + L" (Error: " + std::to_wstring(error) + L")");
    }
    FindClose(hFind);
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
        log(L"Failed to get current directory (Error: " + std::to_wstring(GetLastError()) + L")");
        logFile.close();
        system("pause");
        return 1;
    }
    std::wstring repoPath = currentPath;
    log(L"Repo path set to: " + repoPath);
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
    std::unordered_map<std::wstring, std::vector<ClassInfo>> folderClasses;
    listFiles(repoPath, folderClasses, repoPath, 0);
    analyzeMethodCalls(folderClasses);
    log(L"Writing output for " + std::to_wstring(folderClasses.size()) + L" folders");
    for (const auto& pair : folderClasses) {
        std::wstring folder = pair.first;
        const std::vector<ClassInfo>& classes = pair.second;
        output << L"Folder: " << folder << L"\n";
        for (const ClassInfo& ci : classes) {
            output << L" File: " << ci.fileName << L"\n";
            output << L" " << ci.type << L": " << ci.name << L" (Namespace: " << ci.ns << L")";
            if (!ci.baseClasses.empty() && ci.baseClasses != L"None") output << L" Inherits: " << ci.baseClasses;
            output << L"\n";
            if (!ci.uses.empty()) {
                output << L" Dependencies: " << std::accumulate(ci.uses.begin(), ci.uses.end(), std::wstring(), [](const std::wstring& a, const std::wstring& b) { return a.empty() ? b : a + L", " + b; }) << L"\n";
            }
            if (!ci.fields.empty()) {
                output << L" Fields:\n";
                for (const FieldInfo& fi : ci.fields) {
                    output << L" - " << fi.type << L" " << fi.name << L"\n";
                }
            }
            output << L" Methods:\n";
            for (const MethodInfo& mi : ci.methods) {
                output << L" - " << mi.returnType << L" " << mi.name << L"(" << (mi.args.empty() ? L"" : std::accumulate(mi.args.begin(), mi.args.end(), std::wstring(), [](const std::wstring& a, const std::wstring& b) { return a.empty() ? b : a + L", " + b; })) << L")\n";
                if (!mi.calls.empty()) {
                    std::vector<std::wstring> sortedCalls(mi.calls.begin(), mi.calls.end());
                    std::sort(sortedCalls.begin(), sortedCalls.end());
                    if (sortedCalls.size() > 10) sortedCalls.resize(10);
                    output << L" Calls: " << std::accumulate(sortedCalls.begin(), sortedCalls.end(), std::wstring(), [](const std::wstring& a, const std::wstring& b) { return a.empty() ? b : a + L", " + b; }) << L"\n";
                }
            }
        }
        output << L"\n";
    }
    output.close();
    log(L"Scan complete");
    logFile.close();
    std::wcout << L"Done! Check summary.txt in " << repoPath << L"\n";
    system("pause");
    return 0;
}