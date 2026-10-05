#!/usr/bin/env python3
"""
UnityFramework IL2CPP 注册完整性校验器（离线验尸工具）

用途
----
给定一个已构建的 UnityFramework（Mach-O arm64），判定它是否会在启动时
因 s_Il2CppCodeGenOptions == NULL 而在 MetadataCache::Initialize() 崩溃。

原理
----
Unity 的 Il2CppCodeRegistration.cpp 中，注册对象被宏守护：

    #if RUNTIME_IL2CPP
    static il2cpp::utils::RegisterRuntimeInitializeAndCleanup
        s_Il2CppCodegenRegistrationVariable(&s_Il2CppCodegenRegistration, NULL);
    #endif

若编译时未定义 RUNTIME_IL2CPP=1，该静态变量不会生成，
s_Il2CppCodeGenOptions 全局永远保持 NULL。
运行时 MetadataCache::Initialize() 会执行：

    ldr x8, [x8, #0x5d8]   ; x8 = s_Il2CppCodeGenOptions (NULL)
    ldr w0, [x8, #4]       ; SIGSEGV, far = 0x4

依赖: lief  (pip install lief)

用法
----
    python3 check_il2cpp_reg.py <UnityFramework 路径>

退出码：0 = 正常；1 = 存在缺失，必定崩溃；2 = 无法判定。
"""

import re
import sys


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    path = sys.argv[1]

    try:
        import lief
    except ImportError:
        print('!! 需要 lief：pip install lief')
        return 2

    try:
        fat = lief.MachO.parse(path)
        b = fat.at(0)
    except Exception as e:
        print('!! 解析失败: %s' % e)
        return 2

    syms = [(s.value, s.name) for s in b.symbols]
    print('检查: %s' % path)
    print('符号数: %d' % len(syms))

    names = [n for _, n in syms]
    fail = 0

    # --- 检查 1：IL2CPP 注册符号是否被链接 ---
    reg = [n for n in names if re.search(r'CodegenRegistration', n)]
    print('\n[1] IL2CPP 注册符号（RUNTIME_IL2CPP 是否生效）')
    if reg:
        for n in reg:
            print('    ✓ %s' % n)
    else:
        print('    ✗ 未找到任何 *CodegenRegistration* 符号')
        print('      → 编译时未定义 -DRUNTIME_IL2CPP=1')
        fail = 1

    # --- 检查 2：关键运行期全局 ---
    print('\n[2] 关键运行期全局')
    key = {}
    for want in ('s_Il2CppCodeGenOptions', 's_GlobalMetadataHeader',
                 's_GlobalMetadata', 's_Il2CppCodeRegistration',
                 's_Il2CppMetadataRegistration'):
        hit = [(v, n) for v, n in syms if want in n]
        if hit:
            for v, n in hit:
                key[n] = v
                print('    ✓ 0x%08x  %s' % (v, n))
        else:
            print('    · %s （符号表无，可能已 strip）' % want)

    # --- 检查 3：MetadataCache::Initialize 反汇编，确认崩溃点形态 ---
    print('\n[3] MetadataCache::Initialize 形态检查')
    init = [(v, n) for v, n in syms
            if 'MetadataCache' in n and 'InitializeEv' in n]
    if not init:
        print('    · 未找到该符号，跳过')
    else:
        base = init[0][0]
        print('    函数地址: 0x%x' % base)
        try:
            from capstone import Cs, CS_ARCH_ARM64, CS_MODE_ARM
            tsec = b.get_section('__text')
            data = open(path, 'rb').read()
            off = tsec.offset + (base - tsec.virtual_address)
            code = data[off:off + 0x60]
            md = Cs(CS_ARCH_ARM64, CS_MODE_ARM)
            for insn in md.disasm(code, base):
                print('      0x%06x: %-8s %s'
                      % (insn.address, insn.mnemonic, insn.op_str))
        except ImportError:
            print('    （未装 capstone，跳过反汇编）')
        except Exception as e:
            print('    （反汇编失败: %s）' % e)

    # --- 判定 ---
    print('\n===== 判定 =====')
    if fail:
        print('结论: ✗ 缺失 IL2CPP 注册符号')
        print('      → s_Il2CppCodeGenOptions 将为 NULL')
        print('      → 运行时必定在 MetadataCache::Initialize() 崩溃 (far=0x4)')
        return 1
    print('结论: ✓ IL2CPP 注册符号存在，启动路径应正常')
    return 0


if __name__ == '__main__':
    sys.exit(main())
