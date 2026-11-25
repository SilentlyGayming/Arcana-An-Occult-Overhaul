import argparse
import json
import re
import struct
from pathlib import Path

from dbpf import Key, STBL, fnv64
from trees import ROOT, TREES, WITCH, GENIE
from cs_source import class_text, file_text

CATALOG_DIR = ROOT / "src" / "Localization"
GENERATED = ROOT / "build" / "generated" / "ArcanaEnglish.g.cs"
ENGLISH = "en-US"
LOCALE_IDS = {locale: index for index, locale in enumerate((
    "en-US", "zh-CN", "zh-TW", "cs-CZ", "da-DK", "nl-NL", "fi-FI", "fr-FR",
    "de-DE", "el-GR", "hu-HU", "it-IT", "ja-JP", "ko-KR", "no-NO", "pl-PL",
    "pt-PT", "pt-BR", "ru-RU", "es-ES", "es-MX", "sv-SE", "th-TH",
))}
TABLE_FAMILY = fnv64("Arcana.Localization.Strings") & 0x00FFFFFFFFFFFFFF
PERK_PREFIXES = tuple(tree.text_prefix for tree in TREES)
EXPECTED_TREES = {WITCH.source: (17, 23), GENIE.source: (15, 19)}
NATIVE_BUFF_PREFIX = "Gameplay/Excel/buffs/BuffList:"
NATIVE_BUFF_KEYS = ("Arcana.Witch.Fizzle.Name", "Arcana.Witch.Fizzle.Description",
                    "Arcana.Genie.Exhaustion.Name", "Arcana.Genie.Exhaustion.Description",
                    "Arcana.Ability.genie_lamp.Name", "Arcana.Ability.genie_lamp.Description") + tuple(
    "Arcana.Genie.WishBuff." + name + "." + field
    for name in ("Beauty", "Career", "Joy", "Love", "GoneWrong") + tuple(re.findall(r'new GenieMoodlet\("(\w+)"', class_text(ROOT, "GenieOutcomeBuffs"))) for field in ("Name", "Description"))
KEY = re.compile(r"Arcana(?:\.[A-Za-z0-9_]+)+")
BLESSINGS = ("Fortune", "Vitality", "Inspiration", "Harmony", "Protection")
NOTICE = re.compile(r"Arcana\.Genie\.(?:Blessing\.\w+\.(?:Granted|Resisted)|HeartsDesire\.(?:Response|Fulfilled|Mistaken))")
FIRST_RANKS = (("Witch", "Uninitiated"), ("Genie", "Dormant"), ("Fairy", "Budding"))
TOKEN = re.compile(r"\{(0|[1-9][0-9]*|name|Name)\}")
HEADER = struct.Struct("<4sB2xI6x")
ENTRY = struct.Struct("<QI")


def perk_tiers(tree=WITCH):
    source = class_text(ROOT, tree.source)
    pattern = (
        r'(?m)^\s*Power\("(\w+)",\s*Rank\.\w+,\s*-?\d+,\s*-?[\d.]+f,\s*'
        r'JournalText\.Get\("' + re.escape(tree.text_prefix) + r'\1\.Name"\),\s*'
        r'((?:\d+\s*,\s*)*\d+)\)'
    )
    if tree.source == "GenieTree":
        pattern = (
            r'(?m)^\s*Power\("(\w+)",\s*Rank\.\w+,\s*-?[\d.]+f,\s*'
            r'JournalText\.Get\("' + re.escape(tree.text_prefix) + r'\1\.Name"\),\s*'
            r'((?:\d+\s*,\s*)*\d+)\)'
        )
    tiers = {key: costs.count(",") + 1 for key, costs in re.findall(pattern, source)}
    perks, ranks = EXPECTED_TREES[tree.source]
    if list(tiers) != tree.catalog() or len(tiers) != perks or sum(tiers.values()) != ranks:
        raise ValueError(f"Expected localized names and descriptions for {perks} {tree.occult} perks and {ranks} ranks")
    return tiers


def format_argument_count(source, start):
    brackets = 0
    count = 0
    for token in re.finditer(r'"(?:\\.|[^"\\])*"|[()[\]{},]', source[start:]):
        symbol = token.group()
        if len(symbol) > 1:
            continue
        if symbol in "([{":
            brackets += 1
        elif symbol in ")]}":
            if brackets == 0:
                return count
            brackets -= 1
        elif symbol == "," and brackets == 0:
            count += 1
    raise ValueError("Unclosed JournalText.Format call")


def runtime_keys():
    keys = set()
    formatted = {}
    for path in (ROOT / "src").rglob("*.cs"):
        source = path.read_text(encoding="utf-8")
        keys.update(re.findall(r'"(' + KEY.pattern + r')"', source))
        for match in re.finditer(r'JournalText\.Format\("(' + KEY.pattern + r')"', source):
            key = match.group(1)
            count = format_argument_count(source, match.end())
            numbered = {str(index) for index in range(count)}
            if formatted.setdefault(key, numbered) != numbered:
                raise ValueError(f"{path}: inconsistent format arguments for {key}")
    for tree in TREES:
        for key, count in perk_tiers(tree).items():
            prefix = tree.text_prefix + key
            keys.add(prefix + ".Tagline")
            keys.update(f"{prefix}.Description.{rank}" for rank in range(1, count + 1))
    catalog_source = file_text(ROOT, "SpellCatalog")
    spell_ids = set(re.findall(r'Add\(entries, "([a-z_]+)"', catalog_source))
    for motive in ("bladder", "hunger", "energy", "hygiene", "fun", "social"):
        spell_ids.update(("charm_" + motive, "curse_" + motive))
    for spell_id in spell_ids:
        keys.update(("Arcana.Spell." + spell_id + ".Name", "Arcana.Spell." + spell_id + ".Description"))
    formatted["Arcana.Study.Discovered"] = {"0", "1"}
    formatted["Arcana.Spell.IceOtherTargets"] = {"0"}
    formatted["Arcana.Spell.SunlightOtherTargets"] = {"0"}
    formatted["Arcana.Cheat.UnlockAll.Success"] = {"0", "1"}
    formatted["Arcana.Genie.HeartsDesire.Response"] = {"0", "1"}
    for key in ("HeartsDesire.Fulfilled", "HeartsDesire.Mistaken", "Blessing.Protection.Resisted", "Wish.Granted", "Wish.Failed",
                "Wish.Doom", "Rain.Summoned", "Rain.Failed"):
        formatted["Arcana.Genie." + key] = {"0"}
    for blessing in BLESSINGS:
        keys.update("Arcana.Genie.Blessing." + blessing + "." + part for part in ("Name", "Description", "Granted"))
        keys.update("Arcana.Genie.HeartsDesire." + blessing + "." + str(line) for line in range(1, 5))
        formatted["Arcana.Genie.Blessing." + blessing + ".Granted"] = {"0"}
    for occult, rank in FIRST_RANKS:
        keys.update(("Arcana." + occult + ".Rank.Full." + rank, "Arcana." + occult + ".Rank.Description." + rank))
    formatted["Arcana.Genie.Wishes.DeathEscaped"] = {"0"}
    genie_buff_source = class_text(ROOT, "GenieOutcomeBuffs")
    for name in re.findall(r'new GenieMoodlet\("(\w+)"', genie_buff_source):
        keys.update("Arcana.Genie.WishBuff." + name + "." + part for part in ("Name", "Description"))
    for key in keys:
        if key.startswith(("Arcana.Genie.Wish.Failure.", "Arcana.Genie.Wish.Success.", "Arcana.Genie.Fate.")):
            short = key.rsplit(".", 1)[1]
            if short in ("FortunesDebt",):
                formatted[key] = set()
            elif key in ("Arcana.Genie.Wish.Success.Resurrect","Arcana.Genie.Wish.Success.Love",
                         "Arcana.Genie.Wish.Success.More", "Arcana.Genie.Wish.Success.Fortune",
                         "Arcana.Genie.Wish.Failure.Resurrect", "Arcana.Genie.Fate.ThingYouLoveMost", "Arcana.Genie.Fate.ForgottenLove"):
                formatted[key] = {"0", "1"}
            else:
                formatted[key] = {"0"}
    return keys, formatted


def tokens(text):
    found = set(TOKEN.findall(text))
    remaining = TOKEN.sub("", text)
    if "{" in remaining or "}" in remaining:
        raise ValueError("unsupported placeholder or literal brace")
    numeric = sorted(int(token) for token in found if token.isdigit())
    if numeric and (len(numeric) != len(found) or numeric != list(range(len(numeric)))):
        raise ValueError("numbered placeholders must start at {0}, be consecutive, and stand alone")
    return found


def unique_pairs(pairs):
    catalog = {}
    for key, value in pairs:
        if key in catalog:
            raise ValueError(f"duplicate key {key}")
        catalog[key] = value
    return catalog


def read_catalog(path):
    try:
        catalog = json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique_pairs)
        if not isinstance(catalog, dict) or not catalog:
            raise ValueError("expected a nonempty JSON object")
        hashes = {}
        placeholders = {}
        for key, value in catalog.items():
            if not KEY.fullmatch(key):
                raise ValueError(f"invalid Arcana string key {key!r}")
            string_id = fnv64(key)
            if string_id in hashes:
                raise ValueError(f"FNV64 collision between {hashes[string_id]} and {key}")
            hashes[string_id] = key
            if not isinstance(value, str) or not value.strip() or "\0" in value:
                raise ValueError(f"{key}: expected nonempty text without NUL characters")
            try:
                value.encode("utf-16le")
                placeholders[key] = tokens(value)
            except ValueError as error:
                raise ValueError(f"{key}: {error}") from error
        return catalog, placeholders
    except (OSError, ValueError) as error:
        raise ValueError(f"{path}: {error}") from error


def catalogs(directory=CATALOG_DIR):
    english_path = directory / ENGLISH / "strings.json"
    english, english_tokens = read_catalog(english_path)
    required, formatted = runtime_keys()
    missing, extra = required - english.keys(), english.keys() - required
    if missing or extra:
        raise ValueError(f"{english_path}: missing runtime keys {sorted(missing)}. "
                         f"Unused keys {sorted(extra)}.")
    for key, placeholders in english_tokens.items():
        allowed = formatted.get(key, set())
        if key.startswith(PERK_PREFIXES) and ".Description." in key:
            allowed = {"name"}
        elif key.startswith(("Arcana.Witch.Rank.Description.", "Arcana.Genie.Rank.Description.", "Arcana.Fairy.Rank.Description.")):
            allowed = {"Name"}
        if not placeholders <= allowed or (key in formatted and placeholders != allowed):
            raise ValueError(f"{english_path}: {key} has incompatible placeholders {sorted(placeholders)}")
    result = {ENGLISH: english}
    for path in sorted(directory.glob("*/strings.json")):
        locale = path.parent.name
        if locale == ENGLISH:
            continue
        if locale not in LOCALE_IDS:
            raise ValueError(f"{path}: unknown Sims 3 locale {locale}")
        translated, translated_tokens = read_catalog(path)
        for key, placeholders in translated_tokens.items():
            if key not in english:
                raise ValueError(f"{path}: unrecognized key {key}")
            if placeholders != english_tokens[key]:
                raise ValueError(f"{path}: {key} requires placeholders {sorted(english_tokens[key])}")
        result[locale] = translated
    return result


def table_key(locale):
    return Key(STBL, 0, (LOCALE_IDS[locale] << 56) | TABLE_FAMILY)


def encode_table(catalog):
    output = bytearray(HEADER.pack(b"STBL", 2, len(catalog)))
    for string_id, value in sorted((fnv64(key), value) for key, value in catalog.items()):
        data = value.encode("utf-16le")
        output.extend(ENTRY.pack(string_id, len(data) // 2))
        output.extend(data)
    return bytes(output)


def read_table(data):
    if len(data) < HEADER.size:
        raise ValueError("Truncated STBL header")
    magic, version, count = HEADER.unpack_from(data)
    if (magic, version) != (b"STBL", 2):
        raise ValueError("Expected a Sims 3 version 2 STBL")
    offset = HEADER.size
    result = {}
    for _ in range(count):
        if offset + ENTRY.size > len(data):
            raise ValueError("Truncated STBL entry")
        string_id, length = ENTRY.unpack_from(data, offset)
        offset += ENTRY.size
        end = offset + length * 2
        if end > len(data) or string_id in result:
            raise ValueError("Truncated or duplicate STBL string")
        result[string_id] = data[offset:end].decode("utf-16le")
        offset = end
    if offset != len(data):
        raise ValueError("Extra bytes after the last STBL string")
    return result


def resources(directory=CATALOG_DIR):
    localized = catalogs(directory)
    english = localized[ENGLISH]
    tables = []
    for locale in sorted(localized, key=LOCALE_IDS.__getitem__):
        entries = localized[locale].copy()
        for key in NATIVE_BUFF_KEYS:
            entries[NATIVE_BUFF_PREFIX + key] = entries.get(key, english[key])
        tables.append((table_key(locale), encode_table(entries)))
    return tables


def notification_keys(english):
    prefixes = ("Arcana.Genie.Fate.", "Arcana.Genie.Wish.Success.", "Arcana.Genie.Wish.Failure.")
    keys = {key for key in english if key.startswith(prefixes) or NOTICE.fullmatch(key)}
    for path in (ROOT / "src").rglob("*.cs"):
        source = path.read_text(encoding="utf-8")
        keys.update(re.findall(r'(?:SpellKnowledge\.)?Notify\([^,]+,\s*"(' + KEY.pattern + r')"', source))
    keys.update(key for key in ("Arcana.Genie.Wish.Failed", "Arcana.Genie.Wish.Granted", "Arcana.Genie.Rain.Summoned",
                                "Arcana.Genie.Rain.Failed", "Arcana.Study.Discovered") if key in english)
    return sorted(keys & english.keys())


def generate(destination=GENERATED):
    english = catalogs()[ENGLISH]
    lines = [

        "namespace SupernaturalExpanded.Journal",
        "{",
        "    internal static class ArcanaEnglish",
        "    {",
        "        internal static readonly string[] NotificationKeys =",
        "        {",
        *["            " + json.dumps(key) + "," for key in notification_keys(english)],
        "        };",
        "        internal static string Lookup(string key)",
        "        {",
        "            switch (key)",
        "            {",
    ]
    for key, value in sorted(english.items()):
        lines.append(f"                case {json.dumps(key)}: return {json.dumps(value)};")
    lines.extend([
        "            }",
        "            return null;",
        "        }",
        "    }",
        "}",
        "",
    ])
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes("\n".join(lines).encode("utf-8"))


def main():
    parser = argparse.ArgumentParser(description="Validate the string catalogs and optionally generate the English fallback.")
    parser.add_argument("--generate", type=Path, help="Also write the generated English C# file.")
    args = parser.parse_args()
    for locale, catalog in catalogs().items():
        print(f"{locale}: {len(catalog)} keys -> {table_key(locale)}")
    if args.generate:
        generate(args.generate)


if __name__ == "__main__":
    main()
