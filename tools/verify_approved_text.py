"""Checks that the approved moodlet and pop-up catalogs match strings.json, the generated STBL tables and the developer literals."""
import csv
import re
import sys
from pathlib import Path

from dbpf import fnv64
from localization import ENGLISH, NATIVE_BUFF_KEYS, NATIVE_BUFF_PREFIX, catalogs, read_table, resources, table_key

ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "docs" / "Localization"
CATALOGS = {"Moodlets": DOCS / "Arcana — Moodlets.txt", "Popups": DOCS / "Arcana — Pop-ups.txt"}
COUNTS = {"Moodlets": 29, "Popups": 126}
ORGANIZATIONAL = re.compile(r" \((Beauty|Career)\)$")


def entries(path):
    lines = path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").split("\n")[1:]
    found, block = [], []
    for line in lines + [""]:
        if line.strip():
            block.append(line)
        elif block:
            found.append((block[0], "\n".join(block[1:])))
            block = []
    return found


def expected(catalog, heading, text, field):
    if catalog == "Moodlets" and field == "Name":
        return ORGANIZATIONAL.sub("", heading)
    return text


def main():
    english = catalogs()[ENGLISH]
    table = read_table(dict(resources())[table_key(ENGLISH)])
    rows = list(csv.DictReader((DOCS / "Approved_Text_Mapping.csv").open(encoding="utf-8")))
    approved = {name: entries(path) for name, path in CATALOGS.items()}
    for name, count in COUNTS.items():
        assert len(approved[name]) == count, f"{name}: expected {count} entries, found {len(approved[name])}"
        covered = {int(row["Entry"]) for row in rows if row["Catalog"] == name}
        assert covered == set(range(1, count + 1)), f"{name}: mapping does not cover every entry"
    checked = 0
    for row in rows:
        heading, text = approved[row["Catalog"]][int(row["Entry"]) - 1]
        assert heading == row["Heading"], f"{row['Catalog']} {row['Entry']}: heading drifted from the mapping"
        value = expected(row["Catalog"], heading, text, row["Field"])
        for target in row["Key or source"].split(" | "):
            if target.startswith("Arcana."):
                assert english.get(target) == value, f"strings.json differs from the approved text: {target}"
                assert table.get(fnv64(target)) == value, f"generated STBL differs from the approved text: {target}"
                if target in NATIVE_BUFF_KEYS:
                    assert table.get(fnv64(NATIVE_BUFF_PREFIX + target)) == value, f"moodlet alias differs: {target}"
            else:
                source = (ROOT / target).read_text(encoding="utf-8")
                for piece in re.split(r"\{[A-Za-z]+\}", value):
                    assert not piece or piece in source, f"{target} lacks the approved text {piece!r}"
            checked += 1
    print(f"PASS: {len(approved['Moodlets'])} moodlets and {len(approved['Popups'])} pop-ups, {checked} approved strings "
          "match strings.json, the generated en-US STBL, moodlet aliases and developer literals")


if __name__ == "__main__":
    sys.exit(main())
