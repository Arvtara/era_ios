#!/usr/bin/env python3
"""
il2cpp global-metadata.dat 结构校验器（metadata version 24 / Unity 2019.4）

用途：在 CI 的 export 阶段对 Unity 生成的 global-metadata.dat 做完整性自检，
      把"结构损坏/截断/版本不符"这类问题在打包前拦下。

背景：曾出现设备上 App 启动即崩、崩溃点定格在
        il2cpp::vm::MetadataCache::Initialize()
        ldr w0, [x8, #4]        (x8 = s_GlobalMetadataHeader == NULL)
      的现象。该现象的一个可能成因是 metadata 文件在构建或打包环节被破坏，
      本脚本负责排除"构建侧文件本身有问题"这一分支。

退出码：0 = 校验通过；1 = 发现问题（CI 将 fail）
"""
import struct
import sys

HEADER_PAIRS = [
    "stringLiteral", "stringLiteralData", "string",
    "events", "properties", "methods", "parameterDefaultValues",
    "fieldDefaultValues", "fieldAndParameterDefaultValueData",
    "fieldMarshaledSizes", "parameters", "fields",
    "genericParameters", "genericParameterConstraints",
    "genericContainers", "nestedTypes", "interfaces",
    "vtableMethods", "interfaceOffsets", "typeDefinitions",
    "images", "assemblies", "fieldRefs", "referencedAssemblies",
    "attributesInfo", "attributeTypes",
    "unresolvedVirtualCallParameterTypes",
    "unresolvedVirtualCallParameterRanges",
    "windowsRuntimeTypeNames", "windowsRuntimeStrings",
    "exportedTypeDefinitions",
]

MAGIC = 0xFAB11BAF
EXPECTED_VERSION = 24


def check(path):
    with open(path, 'rb') as fh:
        data = fh.read()
    size = len(data)

    print("文件: %s" % path)
    print("大小: %d 字节 (%.2f MB)" % (size, size / 1048576.0))
    print()

    problems = []

    if size < 8:
        print("::error::文件过小（%d 字节），不可能有效" % size)
        return False

    magic, version = struct.unpack_from('<Ii', data, 0)
    ok_magic = magic == MAGIC
    ok_ver = version == EXPECTED_VERSION
    print("magic   = 0x%08X  %s" % (magic, "OK" if ok_magic else "错误（期望 0x%08X）" % MAGIC))
    print("version = %d  %s" % (version, "OK" if ok_ver else "错误（期望 %d）" % EXPECTED_VERSION))
    if not ok_magic:
        problems.append("magic 不匹配")
    if not ok_ver:
        problems.append("version 不匹配")

    hdr_len = 8 + len(HEADER_PAIRS) * 8
    if size < hdr_len:
        print("::error::文件 %d 字节，不足以容纳头部 %d 字节" % (size, hdr_len))
        return False
    print("头部长度 = %d 字节" % hdr_len)

    print()
    print("%-40s %10s %10s %12s  %s" % ("section", "offset", "size", "end", "status"))
    entries = []
    for i, name in enumerate(HEADER_PAIRS):
        off, sec_size = struct.unpack_from('<ii', data, 8 + i * 8)
        end = off + sec_size
        entries.append((name, off, sec_size, end))

        flags = []
        if sec_size < 0:
            flags.append("size<0")
        if off < 0:
            flags.append("offset<0")
        if sec_size > 0:
            if off < hdr_len:
                flags.append("offset 落在头部内")
            if end > size:
                flags.append("越界")
        print("%-40s %10d %10d %12d  %s" % (name, off, sec_size, end, " ".join(flags) or "OK"))

    oob = [e for e in entries if e[2] > 0 and (e[3] > size or e[1] < 0 or e[2] < 0)]
    if oob:
        for name, off, sec_size, end in oob:
            problems.append("%s 越界 (offset=%d size=%d end=%d 文件=%d)" % (name, off, sec_size, end, size))
        print()
        print("发现 %d 个越界/非法 section" % len(oob))
    else:
        print()
        print("所有 section 均在文件范围内")

    active = sorted([(o, s, n) for n, o, s, e in entries if s > 0 and o > 0], key=lambda x: x[0])
    overlaps = []
    for a, b in zip(active, active[1:]):
        if a[0] + a[1] > b[0]:
            overlaps.append((a[2], a[0] + a[1], b[2], b[0]))
    if overlaps:
        for x in overlaps[:10]:
            problems.append("section 重叠: %s(end=%d) 与 %s(start=%d)" % x)
        print("发现 %d 处 section 重叠" % len(overlaps))
    else:
        print("section 之间无重叠")

    # 取样字符串堆：string 段应为可读 ASCII/UTF-8；
    # stringLiteralData 段存的是原始字面量字节（本工程为 UTF-8 日文），
    # 因此只校验 UTF-8 合法性，不要求可打印 ASCII。
    for name, off, sec_size, end in entries:
        if sec_size <= 16 or off + hdr_len > size:
            continue
        if name == "string":
            chunk = data[off:off + 64]
            printable = sum(1 for c in chunk if 32 <= c < 127 or c == 0)
            print("取样 string              %d/%d 字节可打印" % (printable, len(chunk)))
            if printable < len(chunk) // 4:
                problems.append("string 段取样可打印字符过少，疑似损坏")
        elif name == "stringLiteralData":
            # 切片可能截断多字节字符，用增量解码器只校验"已收到部分"的合法性
            chunk = data[off:off + 96]
            import codecs
            dec = codecs.getincrementaldecoder('utf-8')()
            try:
                dec.decode(chunk, final=False)
                print("取样 stringLiteralData   UTF-8 合法")
            except UnicodeDecodeError:
                problems.append("stringLiteralData 段非合法 UTF-8，疑似损坏")
                print("取样 stringLiteralData   UTF-8 非法")

    print()
    if problems:
        for p in problems:
            print("::error::%s" % p)
        print("==> 结论: metadata 校验未通过")
        return False

    print("==> 结论: metadata 校验通过")
    return True


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("用法: check_metadata.py <global-metadata.dat>")
        sys.exit(1)
    sys.exit(0 if check(sys.argv[1]) else 1)
