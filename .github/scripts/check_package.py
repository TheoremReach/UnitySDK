#!/usr/bin/env python3
"""Checks the package as Unity's Package Manager will take it, with no Unity editor: CI can't run one.

- The versions agree: package.json's, the C# constant, the README's install tag and the changelog's newest entry. So do
  the native SDKs': the Android one the dependency file names, and the iOS one the xcframework declares, against the
  README and the changelog. The README and the changelog name the Unity floor package.json declares.
- The package holds only the files below, so nothing else ships.
- Every file and folder Unity imports has a .meta, every .meta has its asset, and every GUID is unique. None is one the
  cleanup script removes: the old .unitypackages' files, and the folders that held the rest of theirs.
- The xcframework is byte for byte, and mode for mode, the iOSSDK repository's at the tag of its version. Each slice
  declares that version and the same source commit.
- The xcframework is embedded once, from its import settings: no post-build script embeds it again.
- Every iOS function the C# calls is defined in the bridge, inside its extern "C" block and with as many parameters,
  and every one the bridge defines is called. The bridge compiles with ARC.
- Maven Central serves the Android SDK's POM.
"""
import glob
import json
import os
import plistlib
import re
import subprocess
import sys
import tempfile
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
XCFRAMEWORK = 'Plugins/iOS/TheoremReachSDK.xcframework'

# Every file the package ships, besides the xcframework's own, and the folders between them.
FILES = [
    'package.json', 'README.md', 'CHANGELOG.md', 'LICENSE.md',
    'Runtime/TheoremReach.Runtime.asmdef', 'Runtime/TheoremReach.cs', 'Runtime/TheoremReachCallbacks.cs',
    'Editor/TheoremReach.Editor.asmdef', 'Editor/TheoremReachDependencies.xml', 'Editor/TheoremReachLegacyCleanup.cs',
    'Plugins/iOS/TheoremReachUnityBridge.mm',
    'Tests/Runtime/TheoremReach.Tests.asmdef', 'Tests/Runtime/TheoremReachCallbacksTests.cs',
]
FOLDERS = ['Runtime', 'Editor', 'Plugins', 'Plugins/iOS', 'Tests', 'Tests/Runtime']
# Files Unity never imports: the repository's own.
UNIMPORTED = re.compile(r'^(\.github/|\.gitignore$|\.gitattributes$)')

failures = []


def fail(message):
    failures.append(message)
    print(f'::error::{message}')


def read(path):
    with open(os.path.join(ROOT, path), encoding='utf-8') as f:
        return f.read()


def git_files(repo, prefix=''):
    """Each tracked file under prefix, with its git mode."""
    out = subprocess.run(['git', '-C', repo, 'ls-files', '-s', '--', prefix or '.'], check=True,
                         capture_output=True, text=True).stdout
    entries = {}
    for line in out.splitlines():
        meta, path = line.split('\t', 1)
        entries[path] = meta.split()[0]
    return entries


def check_versions():
    package = json.loads(read('package.json'))
    version = package['version']
    csharp = re.search(r'public const string Version = "([^"]+)";', read('Runtime/TheoremReach.cs'))
    if not csharp or csharp.group(1) != version:
        fail(f'TheoremReach.Version is {csharp and csharp.group(1)}, not package.json\'s {version}')
    readme, changelog = read('README.md'), read('CHANGELOG.md')
    if f'UnitySDK.git#{version}' not in readme:
        fail(f'The README\'s install URL doesn\'t name the tag {version}')
    newest = re.search(r'^## (\S+)', changelog, re.M)
    if not newest or newest.group(1) != version:
        fail(f'The changelog\'s newest entry is {newest and newest.group(1)}, not {version}')
    if package.get('unity') != '2022.3' or 'com.unity.external-dependency-manager' not in package.get('dependencies', {}):
        fail('package.json must declare Unity 2022.3 and depend on Unity\'s External Dependency Manager')

    android = re.findall(r'<androidPackage spec="com\.theoremreach:theoremreach:([^"]+)"', read('Editor/TheoremReachDependencies.xml'))
    if len(android) != 1:
        fail(f'The dependency file names the Android SDK {len(android)} times, not once')
        return version, None, None
    with open(os.path.join(ROOT, XCFRAMEWORK, 'ios-arm64/TheoremReachSDK.framework/Info.plist'), 'rb') as f:
        ios = plistlib.load(f)['CFBundleShortVersionString']
    # Prose wraps, so the words are compared with their whitespace collapsed.
    readme_words, changelog_words = ' '.join(readme.split()), ' '.join(changelog.split())
    floor = f'Unity {package.get("unity")}.{package.get("unityRelease")} or later'
    if floor not in readme_words or floor not in changelog_words:
        fail(f'The README or the changelog doesn\'t say "{floor}", the floor package.json declares')
    for name, native in (('Android', android[0]), ('iOS', ios)):
        if f'{name} SDK {native}' not in changelog_words or f'{name} {native}' not in readme_words:
            fail(f'The README or the changelog doesn\'t name {name} {native}, the version the package takes')
    print(f'package {version}, Android SDK {android[0]}, iOS SDK {ios}')
    return version, android[0], ios


def check_files():
    tracked = git_files(ROOT)
    shipped = {path for path in tracked if not UNIMPORTED.match(path)}
    framework = {path for path in shipped if path.startswith(XCFRAMEWORK + '/')}
    expected = set(FILES) | {f'{path}.meta' for path in FILES + FOLDERS + [XCFRAMEWORK]}
    for extra in sorted(shipped - framework - expected):
        fail(f'{extra} isn\'t one of the files the package ships')
    for missing in sorted(expected - shipped):
        fail(f'{missing} is missing')
    for path in sorted(framework):
        if path.endswith('.meta'):
            fail(f'{path}: Unity imports the xcframework as one asset, so nothing inside it has a .meta')
    return tracked


def check_metas(tracked):
    old = set(re.findall(r'"([0-9a-f]{32})"', read('Editor/TheoremReachLegacyCleanup.cs')))
    if len(old) < 90:
        fail(f'The cleanup script lists only {len(old)} GUIDs')
    seen = {}
    for meta in sorted(path for path in tracked if path.endswith('.meta')):
        asset = meta[:-len('.meta')]
        if asset not in tracked and not any(path.startswith(asset + '/') for path in tracked):
            fail(f'{meta} has no asset')
        guid = re.search(r'^guid: ([0-9a-f]{32})$', read(meta), re.M)
        if not guid:
            fail(f'{meta} has no GUID')
            continue
        guid = guid.group(1)
        if guid in seen:
            fail(f'{meta} has the GUID of {seen[guid]}')
        if guid in old:
            fail(f'{meta} has a GUID an old .unitypackage used, which the cleanup script would remove')
        seen[guid] = meta


def check_xcframework(ios):
    tracked = git_files(ROOT, XCFRAMEWORK)
    with tempfile.TemporaryDirectory() as temp:
        subprocess.run(['git', '-c', 'advice.detachedHead=false', 'clone', '--quiet', '--depth', '1', '--branch', ios,
                        'https://github.com/TheoremReach/iOSSDK.git', temp], check=True)
        source = {path[len('TheoremReachSDK.xcframework/'):]: mode
                  for path, mode in git_files(temp, 'TheoremReachSDK.xcframework').items()}
        ours = {path[len(XCFRAMEWORK) + 1:]: mode for path, mode in tracked.items()}
        if source != ours:
            for path in sorted(set(source) | set(ours)):
                if source.get(path) != ours.get(path):
                    fail(f'{XCFRAMEWORK}/{path}: mode {ours.get(path)} here, {source.get(path)} at iOSSDK {ios}')
        for path in sorted(set(source) & set(ours)):
            with open(os.path.join(temp, 'TheoremReachSDK.xcframework', path), 'rb') as theirs, \
                    open(os.path.join(ROOT, XCFRAMEWORK, path), 'rb') as mine:
                if theirs.read() != mine.read():
                    fail(f'{XCFRAMEWORK}/{path} differs from iOSSDK {ios}\'s')
    commits = set()
    for plist in sorted(glob.glob(os.path.join(ROOT, XCFRAMEWORK, '*/TheoremReachSDK.framework/Info.plist'))):
        with open(plist, 'rb') as f:
            info = plistlib.load(f)
        if info.get('CFBundleShortVersionString') != ios:
            fail(f'{plist} declares {info.get("CFBundleShortVersionString")}, not {ios}')
        commits.add(info.get('TR_SOURCE_COMMIT'))
    if len(commits) != 1 or None in commits:
        fail(f'The slices name source commits {sorted(map(str, commits))}, not one')
    print(f'xcframework: byte for byte iOSSDK {ios}, {len(tracked)} files, from {commits}')


def ios_import_settings(asset):
    """Whether a plug-in's import settings enable it for iOS, and its iOS settings, from its .meta."""
    ios = re.search(r'iPhone: iOS\n    second:\n      enabled: (\d)\n      settings:(.*?)\n  (?:- first|userData)',
                    read(asset + '.meta'), re.S)
    return (ios.group(1) == '1', ios.group(2)) if ios else (False, '')


def check_single_embed():
    enabled, settings = ios_import_settings(XCFRAMEWORK)
    if not enabled or 'AddToEmbeddedBinaries: true' not in settings:
        fail('The xcframework\'s import settings must enable it for iOS and embed it')
    for path in FILES:
        if path.endswith('.cs') and re.search(r'PostProcessBuild|AddFileToEmbedFrameworks', read(path)):
            fail(f'{path} embeds frameworks after the build, which the import settings already do')


def parameter_count(parameters):
    parameters = parameters.strip()
    return 0 if parameters in ('', 'void') else parameters.count(',') + 1


def extern_c_block(source):
    """Where the source's last extern "C" { ... } block starts and ends, found by counting braces: the bridge's
    strings and comments hold none."""
    start = source.rfind('extern "C" {')
    if start < 0:
        return 0, 0
    depth = 0
    for index in range(source.index('{', start), len(source)):
        if source[index] == '{':
            depth += 1
        elif source[index] == '}':
            depth -= 1
            if depth == 0:
                return start, index
    return start, len(source)


def check_bridge():
    # The C# binds each function by its name alone, so a function defined outside the bridge's extern "C" block, whose
    # name C++ mangles, fails only at the game's link, and one that takes a different number of arguments only at run
    # time. The block the bridge's own functions are defined in is its last.
    called = {name: parameter_count(parameters) for name, parameters in
              re.findall(r'extern \w+ (TheoremReach_\w+)\(([^)]*)\);', read('Runtime/TheoremReach.cs'))}
    bridge = read('Plugins/iOS/TheoremReachUnityBridge.mm')
    block_start, block_end = extern_c_block(bridge)
    defined = {}
    for definition in re.finditer(r'^\w+ (TheoremReach_\w+)\(([^)]*)\)$', bridge, re.M):
        name = definition.group(1)
        if not block_start < definition.start() < block_end:
            fail(f'The iOS bridge defines {name} outside its extern "C" block, so the C# can\'t find it')
        defined[name] = parameter_count(definition.group(2))
    for name in sorted(called.keys() - defined.keys()):
        fail(f'TheoremReach.cs calls {name}, which the iOS bridge doesn\'t define')
    for name in sorted(defined.keys() - called.keys()):
        fail(f'The iOS bridge defines {name}, which TheoremReach.cs never calls')
    for name in sorted(called.keys() & defined.keys()):
        if called[name] != defined[name]:
            fail(f'TheoremReach.cs passes {name} {called[name]} arguments, and the iOS bridge takes {defined[name]}')
    # It keeps strings in statics, which only ARC holds on to.
    enabled, settings = ios_import_settings('Plugins/iOS/TheoremReachUnityBridge.mm')
    if not enabled or 'CompileFlags: -fobjc-arc' not in settings:
        fail('The iOS bridge\'s import settings must enable it for iOS and compile it with -fobjc-arc')
    print(f'iOS bridge: {len(called)} functions, {sum(called.values())} parameters')


def check_central(android):
    url = (f'https://repo.maven.apache.org/maven2/com/theoremreach/theoremreach/{android}/'
           f'theoremreach-{android}.pom')
    try:
        with urllib.request.urlopen(url, timeout=30) as response:
            if response.status != 200:
                fail(f'Maven Central answers {response.status} for {url}')
    except Exception as error:  # an HTTP error or no answer: either way, Central doesn't serve it
        fail(f'Maven Central doesn\'t serve {url}: {error}')


version, android, ios = check_versions()
check_metas(check_files())
if ios:
    check_xcframework(ios)
check_single_embed()
check_bridge()
if android:
    check_central(android)
if failures:
    sys.exit(f'{len(failures)} problems')
print('The package checks out')
