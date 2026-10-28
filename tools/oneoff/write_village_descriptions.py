#!/usr/bin/env python3
"""Write Middle-earth descriptions for the renamed map villages that still described Calradia.

tools/Apply-MapVillageNames.py renamed 438 settlements and left their descriptions alone: on
2026-10-27, 102 of them still said "sits by a small bay that flows into the larger Gulf of Charas"
or "near Lake Laconis", and every translation also named the old vanilla village (#1042). This
one-off writes DESCRIPTIONS (the repo record of the text) into the LIVE TAOM_Map settlements.xml
`text=` and sets the 12 loc_settlements.xml rows to the same English, then removes those rows from
tools/translation_cache, so the translation backlog (#763) translates the new text.

16 of the villages borrowed another village's description key (castle_village_EN3_2 used
Settlements.Settlement.text.village_EN1_4). Each now gets its own key,
Settlements.Settlement.text.<id>, and its own row in every language, added after its name row; the
borrowed key and its rows stay for the village that owns them.

Byte discipline: binary read and write, each file's BOM and newline kept, every output parsed before
any file is written, a non-.xml backup of each file it changes. Idempotent: a second run writes nothing.

Usage:
  python tools/oneoff/write_village_descriptions.py            # dry run: counts per file
  python tools/oneoff/write_village_descriptions.py --apply    # writes the master, 12 loc files, the cache
"""
import argparse
import datetime
import os
import re
import sys
import xml.etree.ElementTree as ET

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)
import add_map_villages as amv  # noqa: E402

KEY = "Settlements.Settlement.text."

DESCRIPTIONS = {
    # Dunland (Culture.empire)
    "castle_village_EN3_1": "Brynbuarth sits on a hill below Tûr Morva. The Dunlendings dig red clay from the stream banks and fire it into pots and roof tiles.",
    "castle_village_EN3_2": "Maeswen lies in a sheltered dale below Tûr Morva. Its shaggy hill ponies are small but sure-footed, and the clans of Dunland trade them as far as the Isen.",
    "castle_village_EN4_1": "Brynmawr sits on a broad hill above Lhanuch. The Dunlendings graze black cattle on its slopes and drive them down to market each autumn.",
    "castle_village_EN4_2": "Waunfawr lies on a wide moor near Lhanuch. Its flocks give the coarse grey wool that Dunlending weavers make into cloaks against the mountain rain.",
    "castle_village_EN5_1": "Nantglas lies in a green valley under the southern slopes of the Misty Mountains. Its woodcutters fell pine and oak at the edge of the old forests of Enedwaith.",
    "castle_village_EN5_2": "Bryncoch sits on a red-earthed hill in the foothills of the Misty Mountains. Its herders keep their cattle close, for orcs and wolves come down from the heights in hard winters.",
    "castle_village_EN6_1": "Nanthalen lies in a narrow valley near Barnavon, where brine springs rise from the rock. The villagers boil the water down in iron pans and sell the salt across Dunland.",
    "castle_village_EN6_2": "Bryncelyn sits among holly-covered hills near Barnavon. The Dunlendings have grazed cattle here since long before the Men of Gondor came to the Isen.",
    "castle_village_EN7_1": "Bryndu lies on a dark hillside near Thror's Coomb, where the exiled Dwarves of Durin's folk once dwelt. Its herders still trade hides and cheese with passing Dwarves.",
    "castle_village_EN7_2": "Coedmawr stands at the edge of a great wood near Thror's Coomb. Its woodcutters sell timber and charcoal to the Dwarves who still keep forges in the hills.",
    "castle_village_EN8_1": "Maesgwyn lies among fields of barley and oats near Dûvodaiad. It is one of the few villages in Dunland where the soil is deep enough to plough.",
    "castle_village_EN8_2": "Glynhaearn sits in a wooded glen near Dûvodaiad. Its miners dig iron from the hillsides, and the clans forge it into the spears they carry to war.",
    "village_EN1_1": "Cwmhaearn lies in a steep valley above Galtrev. Its iron mines are old, and the Dunlendings say their first shafts were cut by their grandfathers' grandfathers.",
    "village_EN1_2": "Dolwen lies in a fair meadow below Galtrev. Streams from the hills water its fields of barley and oats, which feed the market at Galtrev.",
    "village_EN1_3": "Aberlyn sits where a hill stream meets a quiet river near Galtrev. Its fishermen set wicker traps for trout and eels.",
    "village_EN2_1": "Aberglas lies at the mouth of a cold blue stream near Carreglyn. The villagers fish from small hide boats and dry their catch on racks along the shore.",
    "village_EN2_2": "Nant Arian sits by a stream near Carreglyn whose bed glitters with silver ore. The clan chiefs guard the mines in the hills above it closely.",
    "village_EN3_1": "Dolgoch lies in a red-soiled meadow near Lhan Tarren. Its farmers grow barley and oats and brew a strong dark ale that is drunk at every clan feast.",
    # Mordor
    "castle_village_ES1_1": "Dûrthrak lies in the shadow of the Morannon. Slaves fell the last thorny trees of the region and haul the timber to the Black Gate for its forges and siege engines.",
    "castle_village_ES1_2": "Nûrnhai is a slave farm in a sheltered hollow behind the Morannon. Its grain feeds the garrison of the Black Gate.",
    "castle_village_ES2_1": "Gorthûm lies on the plain below Carach Angren. Its slaves plough the ashen soil, and what grain grows here goes to the orcs of the Isenmouthe.",
    "castle_village_ES2_2": "Ang-mauz sits at the foot of the Ash Mountains near Carach Angren. Its iron mines feed the furnaces that arm the hosts of Mordor.",
    "castle_village_ES3_1": "Bûrzum lies in a dark valley below Cirith Ungol. Its orcs keep swine in filthy pens and feed them on the refuse of the tower.",
    "castle_village_ES3_2": "Dûr-mauz sits in a narrow cleft of the Mountains of Shadow near Cirith Ungol. Slaves dig silver from the black rock under the whips of their overseers.",
    "castle_village_ES4_1": "Lûgnen lies on the bitter shore of the Sea of Núrnen. Its slaves fish the dark waters and salt the catch for the armies of the north.",
    "castle_village_ES4_2": "Nûrn-mad lies on the fertile plains of Nurn near Mornaur. Its slave fields are among the many that feed the hosts of the Dark Lord.",
    "castle_village_ES5_1": "Bûrz-glob sits on a river bank near Barad Nûrn. Slaves dig the grey clay and fire it into bricks for the walls of Mordor.",
    "castle_village_ES5_2": "Gûl-salth lies on the salt flats near Barad Nûrn. Slaves rake salt from the shallow pans while overseers watch from wooden towers.",
    "castle_village_ES6_1": "Krimp-bûrz sits in the hills near Cirith Nargil. Its mines yield iron for the smithies of Mordor, and few of the slaves who dig it see the sky again.",
    "castle_village_ES6_2": "Naergûr lies on stony slopes near Cirith Nargil. Its slave herders keep thin sheep whose wool is woven into cloaks for the orc host.",
    "castle_village_ES7_1": "Taur-bûrz lies at the edge of a twisted wood near Barad Wath. Slaves cut its dark trees for the forges and the war engines of Mordor.",
    "castle_village_ES7_2": "Gûl-nûrn lies on the plains of Nurn near Barad Wath. Its grain is stored in walled granaries and sent north to Gorgoroth.",
    "castle_village_ES8_1": "Sereg-nûrn lies on the warm plains near Lûglurag. Slaves tend its wheat from dawn to dusk under the eyes of orc overseers.",
    "castle_village_ES8_2": "Naer-mauz sits in a deep canyon near Lûglurag. Its silver goes to Barad-dûr, where it buys the service of Easterling and Haradrim chieftains.",
    "village_ES1_2": "Bûrz-salth lies on a bitter lake bed in the shadow of Barad-dûr. Slaves cut blocks of salt for the stores of the Dark Tower.",
    "village_ES1_3": "Snagador lies on the plateau of Gorgoroth near Barad-dûr. Its slaves herd hardy sheep on the scant grass between the ash fields.",
    "village_ES1_4": "Angbûrz sits on the slopes of the Ash Mountains near Barad-dûr. Its iron is carried straight to the forges of the Dark Tower.",
    "village_ES2_2": "Nûrn-hoth lies in the valley below Minas Morgul. Its pale fields grow grain for the Morgul host, and the slaves who work them dread the night.",
    "village_ES2_3": "Sereg-had lies on the eastern slopes of the Mountains of Shadow near Minas Morgul. Its fields are worked by slaves taken in raids on Ithilien.",
    "village_ES3_1": "Roch-bûrz lies on the plain below Durthang. Its slaves breed black horses for the riders of Mordor.",
    "village_ES3_2": "Naerhad lies near Durthang, where the plain climbs toward the mountains. Its cattle are lean, and most of their meat goes to the orc garrison.",
    "village_ES4_1": "Bûrznar lies on a rocky ridge near Seregost. Its slave herders drive their flocks between the thin pastures of southern Nurn.",
    "village_ES4_3": "Morroch lies on the eastern grasslands near Seregost. The horses bred here carry the messengers and raiders of Mordor.",
    "village_ES5_1": "Gorthnar lies on the dry plains near Thaurband. Its slaves keep sheep for wool and meat, and the wolves take more of the flock than the shepherds can count.",
    "village_ES5_2": "Dûr-salth lies near Thaurband, where salt springs rise from the ground. Slaves boil the brine in great iron pans that burn day and night.",
    "village_ES5_3": "Angnaur sits in a low pass near Thaurband. Fires burn day and night at the mouths of its iron mines.",
    "village_ES6_1": "Gûl-glob lies beneath grey bluffs near Naerband. Its slaves dig clay from the river bank and press it into bricks for the fortresses of Nurn.",
    "village_ES6_2": "Gorth-nûrn lies along a slow river near Naerband. Its fields are among the richest in Nurn, and all their harvest feeds the armies of the Dark Lord.",
    # Gondor
    "castle_village_EW9_2": "Amrúnbar lies on the eastern shore of the isle of Tolfalas. Its herders graze cattle on the grassy downs above the sea and ship salted beef to Dol Amroth.",
    "village_EW6_1": "Falasbar is a fishing village on the coast of Anfalas near Lond Cirion. Its boats sail at dawn and return with herring and cod for the markets of the coast.",
    "village_EW8_2": "Parthlann lies on wide pastures in the Pinnath Gelin near Ost Arndir. Its herders raise cattle on the green hills and drive them to market along the coast road.",
    "village_EW8_3": "Iaulad lies on gentle slopes near Ost Arndir, in the Green Hills of Pinnath Gelin. Its golden wheat fields feed the towns of the western coast.",
    # Isengard
    "castle_village_isengard_a": "Nan Angren lies in the valley of the Isen near the gates of Isengard. Its herders keep cattle for the garrison of the Wizard, and few of them now go near the walls at night.",
    "village_isengard_a": "Curunlad lies on the plain of Nan Curunír below Orthanc. Its cattle once grazed among green trees, but the woods have been cut to feed the furnaces of Saruman.",
    # Rohan (Culture.vlandia)
    "castle_village_V1_1": "Mearhtun lies on the open grasslands near Marton. Its herds of horses are counted among the finest of the Riddermark.",
    "castle_village_V2_1": "Cornstede sits in the fertile lands near Cliving. Its farmers grow wheat and barley, and much of the grain feeds the riders of the king.",
    "castle_village_V2_2": "Hengestun lies by a bend of a quiet river near Cliving. Its folk breed and train horses for the éoreds of the Mark.",
    "castle_village_V3_1": "Fischam lies in the marshes of the Fenmarch, where slow streams wander through the reeds toward the Entwash. Its fishermen take eels and pike from the reed beds.",
    "castle_village_V3_2": "Stodham lies on dry ground at the edge of the Fenmarch. Its horses graze on meadows won back from the marshes.",
    "castle_village_V4_1": "Horsham lies by a fast stream that runs down from the White Mountains near Starkmoore. Its riders train young horses on the slopes above the village.",
    "castle_village_V5_1": "Fleaxham lies in the damp lowlands near Hiltbolt. Its folk grow flax and weave the linen that the women of the Mark prize.",
    "castle_village_V5_2": "Eofeld lies on a sheltered field near Hiltbolt. Its horses are hardy, bred to bear the cold winds that blow down from the mountains.",
    "castle_village_V6_2": "Huntham lies in the wooded hills near Caleus Castle. Its trappers hunt fox and marten and sell the furs at the markets of the Mark.",
    "castle_village_V7_1": "Feldham lies among wide fields near Gramburg. Its farmers grow wheat and rye for the halls of the Rohirrim.",
    "castle_village_V7_2": "Grafton lies in the stony hills near Gramburg. Its iron mines supply the smiths who make the spears and helms of the Riders of Rohan.",
    "village_V1_1": "Eoworth lies on the plains below Edoras. The king's own herds graze here, and the finest horses of the Mark are trained in its paddocks.",
    "village_V2_1": "Wealdham sits on the wooded slopes of the White Mountains near Helm's Deep. Its woodcutters fell pine and fir for the folk of the Westfold.",
    "village_V2_2": "Æcertun lies on the fields of the Westfold near Helm's Deep. Its farmers grow wheat and barley along the Deeping-stream.",
    "village_V3_2": "Swintun lies in the oak woods of the Eastfold near Aldburg. Its swineherds drive their hogs into the forest to feed on acorns.",
    "village_V3_3": "Berewic lies on the gentle hills of the Eastfold near Aldburg. Its farmers grow barley and wheat for the hall of the Marshal.",
    "village_V5_1": "Oreham lies on a rocky shelf near Aldenburg. Its silver mines are among the few in the Mark, and their silver is worked into the cups and brooches of its lords.",
    "village_V5_2": "Merham lies by a quiet mere near Aldenburg. Its fishermen take pike and perch from the still water.",
    "village_V6_1": "Rygeham lies in a long valley near Langhold. Its farmers grow rye and barley on the warm southern slopes.",
    "village_V6_2": "Neatham lies on green pastures near Langhold. Its cattle grow fat on the rich grass of the Mark.",
    "village_V6_3": "Wicgham lies at the foot of the hills near Langhold. Its stud is old, and its folk boast that its horses have carried Marshals of the Mark.",
    "village_V7_1": "Eoleah lies on the grassy meadows near Grimslade. The land is fine pasture for the horses of the Rohirrim.",
    "village_V7_2": "Wudetun lies by a creek that runs down from the hills near Grimslade. Its woodcutters work the scattered woods, which are rare and precious in the Mark.",
    # Dale (Culture.sturgia)
    "castle_village_S1_1": "Nautby lies on the windswept downs near Westoft. Its hardy cattle are bred to graze through the cold northern winters.",
    "castle_village_S1_2": "Hrossdal lies in a grassy valley near Westoft. The Men of Dale raise sturdy horses here for their riders and their wagons.",
    "castle_village_S2_1": "Linby lies in the damp valleys near Garthness. Its folk grow flax and weave fine linen that is traded down the River Running.",
    "castle_village_S2_2": "Hestby lies among rye and barley fields near Garthness, at the edge of the wild woods. Its landholders raise horses on pastures cleared from the forest.",
    "castle_village_S3_1": "Kornby lies on a flat shelf of land near Bridgethorp. The land is cold, but the hills wash down rich soil, and its rye and barley feed the towns of Dale.",
    "castle_village_S3_2": "Stodby lies on high pastures near Bridgethorp. Its horses graze on the heights above the river.",
    "castle_village_S4_1": "Skinnby lies in a forested valley near Tham Aeldir. Its trappers hunt marten and beaver and sell the furs to merchants from Esgaroth.",
    "castle_village_S4_2": "Nautdal lies on grassy downs between two small rivers near Tham Aeldir. Its folk are cattle herders, and their cheese is known as far as Esgaroth.",
    "castle_village_S5_1": "Jarnby is a young village in the wild hills near Torndal. Its settlers came for furs and stayed for the iron in the rock.",
    "castle_village_S5_2": "Veidby lies at the edge of the wild woods near Torndal. It is a base for the hunters who go deep into the forest after furs.",
    "castle_village_S6_1": "Nautfell lies on a high pass near Taigbarn. Its herders guard their cattle against the wolves that come down from the Grey Mountains.",
    "castle_village_S6_2": "Fiskdal lies above the rapids of a swift river near Taigbarn. Its fishermen climb down the cliffs each morning to set their nets.",
    "castle_village_S7_1": "Vikby lies on a cold bay near Yoldbryn. Its fishermen take pike and trout from the deep water and smoke them for the winter.",
    "castle_village_S7_2": "Akrdal lies on high ground near Yoldbryn. Its farmers grow rye and barley in the short northern summers.",
    "village_S1_1": "Veidholt lies on a wooded ridge above the city of Dale. Its hunters trap fox and marten on the slopes of the Lonely Mountain.",
    "village_S1_3": "Fiskvik lies on a bend of the River Running below Dale. Its fishermen sell their catch in the market of the city.",
    "village_S2_1": "Akrby lies on a ridge near Stranding. Its farmers grow wheat just inland of the shore, sheltered from the wind off the water.",
    "village_S2_2": "Jarnfell lies on a high rocky ridge near Stranding. It is a harsh land, settled only for the iron that the Men of Dale dig from its hills.",
    "village_S3_1": "Engby lies in meadows cleared from the forest near Eldby. Its farmers grow wheat and oats on the dark, rich soil.",
    "village_S3_2": "Fiskby lies on a point of land near Eldby. Its fishermen take perch and bream from the shallow bay.",
    "village_S4_1": "Vatnby lies on a small rise above the water near Liutburg. The village lives by its nets, which bring in perch, bream and salmon.",
    "village_S4_3": "Jarndal lies where the shore rises to meet the hills near Liutburg. Its miners dig iron and sell it to the smiths of Dale and the Dwarves of Erebor.",
    "village_S4_4": "Skogdal lies in a fold of the hills near Liutburg. Its woodcutters fell oak and elm and float the logs down to Esgaroth.",
    "village_S5_1": "Saudby is a sheep-rearing village on the lower slopes near Vargfell. Its wool is woven into the warm cloaks that the Men of Dale wear in winter.",
    "village_S5_2": "Fedal lies on the eastern slopes near Vargfell, close to the wild lands of Rhovanion. Its herders raise highland cattle bred to bear the icy winds.",
}


def escape(text):
    return text.replace("&", "&amp;").replace("<", "&lt;").replace('"', "&quot;")


def rewrite_master(master_text, descriptions):
    """`master_text` with each id's text= set to its own key and description; (text, changed ids)."""
    changed = 0
    for sid, desc in descriptions.items():
        pat = re.compile(r'(<Settlement id="' + re.escape(sid) + r'"[^>]*?\stext=")\{=[^}]*\}[^"]*(")')
        m = pat.search(master_text)
        if not m:
            raise SystemExit(f"{sid}: no <Settlement> row with a text= attribute")
        value = "{=" + KEY + sid + "}" + escape(desc)
        new = m.group(1) + value + m.group(2)
        if master_text[m.start():m.end()] != new:
            master_text = master_text[:m.start()] + new + master_text[m.end():]
            changed += 1
    return master_text, changed


def rewrite_loc(loc_text, descriptions):
    """`loc_text` with each id's own description row set to the English, added after its name row when absent."""
    changed = 0
    nl = amv.detect_newline(loc_text)
    for sid, desc in descriptions.items():
        value = escape(desc)
        own = re.compile(r'(<string id="' + re.escape(KEY + sid) + r'"\s+text=")([^"]*)(")')
        m = own.search(loc_text)
        if m:
            if m.group(2) != value:
                loc_text = loc_text[:m.start(2)] + value + loc_text[m.end(2):]
                changed += 1
            continue
        name = re.search(r'([ \t]*)<string id="Settlements\.Settlement\.name\.' + re.escape(sid) + r'"[^>]*/>', loc_text)
        if not name:
            raise SystemExit(f"{sid}: no name row to place its description after")
        at = loc_text.find("\n", name.end()) + 1
        row = f'{name.group(1)}<string id="{KEY}{sid}" text="{value}" />{nl}'
        loc_text = loc_text[:at] + row + loc_text[at:]
        changed += 1
    return loc_text, changed


def main(argv=None):
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write the live files and the cache (default: dry run)")
    args = ap.parse_args(argv)

    files = {"settlements.xml": (amv.LIVE, *amv._read(amv.LIVE))}
    files.update({lang: (amv._loc_path(lang), *amv._read(amv._loc_path(lang))) for lang in amv.LANGS})
    out = {}
    for key, (path, bom, text) in files.items():
        new, n = (rewrite_master if key == "settlements.xml" else rewrite_loc)(text, DESCRIPTIONS)
        ET.fromstring(new.encode("utf-8"))  # every output parses before any file is written
        out[key] = new
        print(f"  {key:15}: {n} description(s) to write")
    if all(out[k] == files[k][2] for k in files):
        print("Every description is already written; nothing to do.")
        return 0
    if not args.apply:
        print("\nDRY RUN: re-run with --apply to write the live files and remove the rows from the cache.")
        return 0
    tag = "descriptions_" + datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    for key, (path, bom, text) in files.items():
        if out[key] != text:
            amv._write(path, bom, out[key], tag)
    removed = amv.evict_cached_names({lang: list(DESCRIPTIONS) for lang in amv.LANGS}, field="text")
    print(f"\nApplied; backups *.bak_{tag}; {sum(removed.values())} cache row(s) removed. "
          "Then run python tools/check_external_loc_coverage.py --update-baseline.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
