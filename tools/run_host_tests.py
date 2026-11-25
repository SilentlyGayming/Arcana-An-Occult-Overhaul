"""Runs Arcana's C# formula, tree, Spell Knowledge and Weather Stone checks on the host with the .NET 8 SDK."""
import argparse
import os
import shutil
import struct
import subprocess
import sys
from pathlib import Path
from tempfile import TemporaryDirectory
from xml.sax.saxutils import escape

import cs_source
import localization

ROOT = Path(__file__).resolve().parents[1]
TESTS = ROOT / 'tools' / 'tests'
GAME_ASSEMBLIES = ('Sims3GameplaySystems', 'Sims3GameplayObjects', 'Sims3MetaData', 'SimIFace', 'UI')
GENIE_TREE_TYPES = ('Rank', 'Perk', 'PerkConnection', 'PerkPath', 'OccultTree', 'PageControl',
                    'TooltipModifier', 'PerkText', 'RankState', 'PerkProgression', 'PerkRecord',
                    'PerkLedger', 'GenieProgressStore', 'GenieTree', 'GeniePerkBenefits',
                    'PowerProgress', 'PowerSelection', 'WindowGeometry', 'PowerLayout', 'PercentText')
WEATHER_TYPES = ('FortuneRainState', 'StoneSummonProgress', 'WeatherStoneTuning', 'NativeWeatherCalls',
                 'RainExposure', 'Rank', 'RankState', 'PerkProgression')
HOST_TEXT = '''
internal delegate TooltipModifier[] PerkBenefits(Perk perk, int descriptionIndex);
internal delegate Dictionary<ulong, List<int>> RecordTable();
internal delegate bool RecordMigration(PerkRecord record);
internal static class PerkProgressSource { internal const long NoExperience = -1L; }
internal static class JournalText {
    internal static string Get(string key) { return ArcanaEnglish.Lookup(key) ?? "Text unavailable"; }
    internal static string Format(string key, params object[] values) { return string.Format(Get(key), values); }
}
internal static class OverviewProviders {
    internal static string GetRomanNumeral(int value) { return new[] { "", "I", "II", "III", "IV", "V" }[value]; }
}
'''


def rules_sources():
    return [cs_source.file_path(ROOT, name) for name in ('SpellCatalog', 'CastingRules', 'CasterProfile', 'SpellResearchSettings')]


def wrap(path, usings, body):
    path.write_text(usings + ' namespace SupernaturalExpanded.Journal {\n' + body + '\n}')
    return path


def method_text(source, signature):
    start = source.index(signature)
    end = source.index('{', start) + 1
    depth = 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def host_copy(source, target):
    data = bytearray(source.read_bytes())
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    sections = struct.unpack_from('<H', data, pe + 6)[0]
    optional_size = struct.unpack_from('<H', data, pe + 20)[0]
    optional = pe + 24
    cli = struct.unpack_from('<I', data, optional + 96 + 14 * 8)[0]
    for i in range(sections):
        row = optional + optional_size + i * 40
        virtual_size, start, raw_size, raw = struct.unpack_from('<IIII', data, row + 8)
        if start <= cli < start + max(virtual_size, raw_size):
            offset = raw + cli - start + 16
            struct.pack_into('<I', data, offset, struct.unpack_from('<I', data, offset)[0] & ~2)
            target.write_bytes(data)
            return
    raise ValueError('No CLR header found')


class Host:
    def __init__(self, args, folder):
        self.dotnet = args.dotnet or shutil.which('dotnet')
        if not self.dotnet:
            raise RuntimeError('The host tests need the .NET 8 SDK (use --dotnet PATH).')
        self.refs = args.game_assemblies.resolve()
        self.args = args
        self.folder = folder
        (folder / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')

    def references(self, names):
        items = ''
        for name in names:
            path = self.refs / (name + '.dll')
            if not path.is_file():
                raise ValueError('Missing Sims 3 reference: ' + str(path))
            items += '<Reference Include="' + name + '"><HintPath>' + escape(str(path)) + '</HintPath></Reference>'
        return items

    def english(self):
        path = self.folder / 'ArcanaEnglish.g.cs'
        if not path.exists():
            localization.generate(path)
        return path

    def run(self, name, sources, references=GAME_ASSEMBLIES, extra=()):
        items = ''.join('<Compile Include="' + escape(str(path), {'"': '&quot;'}) + '" />' for path in sources)
        project = self.folder / (name + '.csproj')
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>' + self.args.framework + '</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
            '<UseSharedCompilation>false</UseSharedCompilation><NoWarn>1701;0649</NoWarn></PropertyGroup><ItemGroup>'
            + items + self.references(references) + '</ItemGroup></Project>')
        env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1')
        command = [self.dotnet, 'run', '--configuration', 'Release', '--project', str(project)]
        subprocess.run(command + (['--'] + list(extra) if extra else []), env=env, check=True)


def genie_tree(host):
    apply = method_text(cs_source.class_text(ROOT, 'GeniePerks'),
                        'internal static CastingRules Apply(PerkRecord record, CastingRules rules)')
    body = HOST_TEXT + '\n'.join(cs_source.class_text(ROOT, name) for name in GENIE_TREE_TYPES)
    body += '\ninternal static class GeniePerks {\n' + apply + '\n}'
    production = wrap(host.folder / 'GenieTree.cs', 'using System; using System.Collections.Generic; using System.Globalization; '
                      'using Sims3.SimIFace;', body)
    host.run('GenieTree', [production, host.english()] + rules_sources() + [TESTS / 'GenieTreeTests.cs'], ('SimIFace',))


def spell_knowledge(host):
    command = wrap(host.folder / 'UnlockCommand.cs', 'using System;',
                   cs_source.class_text(ROOT, 'SpellUnlockCommand') + '\n' + cs_source.class_text(ROOT, 'SpellResearchClock'))
    adapter = host.folder / 'AdapterHost.dll'
    host_copy(host.args.assembly.resolve(), adapter)
    host.run('SpellKnowledge', [cs_source.file_path(ROOT, 'SpellCatalog'), cs_source.file_path(ROOT, 'SpellKnowledgeSkill'),
                                command, TESTS / 'SpellKnowledgeTests.cs'], extra=(str(adapter),))


def casting_rules(host):
    support = wrap(host.folder / 'RegressionSupport.cs', 'using System;',
                   '\n'.join(cs_source.class_text(ROOT, name) for name in ('Rank', 'RankState', 'PerkProgression')))
    host.run('CastingRules', [support, host.english()] + rules_sources()
             + [cs_source.file_path(ROOT, 'JournalText'), TESTS / 'CastingRulesTests.cs'])


def weather_stone(host):
    state = wrap(host.folder / 'RainState.cs', 'using System; using System.Reflection; using Sims3.Gameplay.Actors; '
                 'using Sims3.Gameplay.Autonomy;', '\n'.join(cs_source.class_text(ROOT, name) for name in WEATHER_TYPES))
    host.run('WeatherStone', [state, TESTS / 'WeatherStoneRainTests.cs', cs_source.file_path(ROOT, 'CastingRules'),
                              cs_source.file_path(ROOT, 'CasterProfile'), cs_source.file_path(ROOT, 'SpellResearchSettings'),
                              cs_source.file_path(ROOT, 'SpellCatalog')])


def weather_stone_cheat(host):
    stone = cs_source.class_text(ROOT, 'StoneAccess')
    methods = method_text(stone, 'internal static bool TryWeather(Sim actor, out WeatherStone.SummonWeatherType kind, out string weather)')
    methods += method_text(stone, 'internal static bool ActivateSelectedWeather(WeatherStone stone, Sim actor, out string weather, out string reason)')
    begin = method_text(cs_source.class_text(ROOT, 'GenieRain'), 'internal static void Begin(WeatherStone stone, Sim genie)')
    support = wrap(host.folder / 'WeatherCheatBridge.cs',
        'using System; using Sims3.Gameplay.Actors; using Sims3.Gameplay.Objects.Environment; '
        'using Sims3.Gameplay.Seasons; using Sims3.Gameplay.Utilities; using Sims3.Gameplay.Core; '
        'using Sims3.Gameplay.EventSystem; using Sims3.SimIFace; using Sims3.SimIFace.Enums; using Sims3.UI.Hud;',
        cs_source.class_text(ROOT, 'FortuneRainState') + '\npartial class GenieRain {' + begin +
        '\n private const float WindowHours=12; }\npartial class StoneAccess {' + methods + '}')
    host.run('WeatherStoneCheat', [cs_source.file_path(ROOT, 'WeatherStoneCheat'), support,
                                 TESTS / 'WeatherStoneCheatTests.cs'], ())


def wish_system(host):
    support = wrap(host.folder / 'WishSupport.cs', 'using System; using Sims3.Gameplay.Careers; using Sims3.Gameplay.Utilities; using Sims3.SimIFace;',
        '\n'.join(cs_source.class_text(ROOT, name) for name in ('Rank', 'RankState', 'PerkProgression', 'WishRecord', 'BlessingRules'))
        + '\ninternal static class WishBuffs {'
        + ' internal static void RestoreTravel(Sims3.Gameplay.Actors.Sim s, float w, int m, float b, float l, ulong t, float j) {}'
        + ' internal static void CaptureTravel(Sims3.Gameplay.Actors.Sim s, out float w, out int m, out float b, out float l, out ulong t, out float j) { w=b=l=j=0; m=0; t=0; } }'
        + '\ninternal static class OccultAssetData { internal const ulong GenieSkillStatus = 0xF5E71684E4B2C36Aul; internal const ulong GenieLifeStateArt = 0x1F9FE1E94FE3CEFAul; }')
    adapter = host.folder / 'WishAdapterHost.dll'
    host_copy(host.args.assembly.resolve(), adapter)
    memory = host.folder / 'WishMemoryStream.cs'
    shared = (TESTS / 'SpellKnowledgeTests.cs').read_text().split('internal sealed class MemoryStreamData', 1)[1]
    memory.write_text('using System.Collections.Generic; using Sims3.SimIFace; internal sealed class MemoryStreamData' + shared)
    host.run('WishSystem', [support, memory, cs_source.file_path(ROOT, 'WishCatalog'), cs_source.file_path(ROOT, 'WishEffectSkill'),
        cs_source.file_path(ROOT, 'GeniePowerPolicy'), cs_source.file_path(ROOT, 'WishReservation')] + rules_sources()
        + [TESTS / 'WishSystemTests.cs'], extra=(str(adapter),))


def wish_menu(host):
    adapter = host.folder / 'WishMenuAdapterHost.dll'
    host_copy(host.args.assembly.resolve(), adapter)
    host.run('WishMenu', [TESTS / 'GenieWishMenuTests.cs'], extra=(str(adapter),))


SUITES = {'genie-tree': genie_tree, 'spell-knowledge': spell_knowledge, 'casting-rules': casting_rules, 'weather-stone': weather_stone, 'weather-stone-cheat': weather_stone_cheat, 'wish-system': wish_system, 'wish-menu': wish_menu}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('suites', nargs='*', help='Suites to run, from ' + ', '.join(sorted(SUITES)) + '; all of them by default.')
    parser.add_argument('--framework', choices=('net8.0', 'net9.0', 'net10.0'), default='net8.0')
    parser.add_argument('--dotnet')
    parser.add_argument('--game-assemblies', type=Path, default=ROOT / '.deps/Sims3')
    parser.add_argument('--assembly', type=Path, default=ROOT / 'build/Arcana - An Occult Overhaul.dll',
                        help='The built mod, which the Spell Knowledge suite loads.')
    args = parser.parse_args()
    unknown = [name for name in args.suites if name not in SUITES]
    if unknown:
        parser.error('unknown suite: ' + ', '.join(unknown))
    with TemporaryDirectory(prefix='arcana-host-tests-') as temporary:
        host = Host(args, Path(temporary))
        for name in args.suites or sorted(SUITES):
            print('Running ' + name + ' checks', flush=True)
            SUITES[name](host)
    return 0


if __name__ == '__main__':
    sys.exit(main())
