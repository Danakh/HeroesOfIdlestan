"""
Generateur des SVG provisoires de l'habillage « warrior » du squelette « humanoid ».

Ces SVG ne sont que des formes simples, en attendant les vrais dessins (Claude Design). Ils
respectent la convention de design/07-animation.md : chaque morceau est dessine dans le repere du
personnage en pose de repos, avec la meme viewBox pour tous. Pour cela, le script relit le
squelette, calcule la matrice monde de repos de chaque os et y place des formes decrites dans le
repere de l'os (X le long de l'os).

    python assets/animation/generate_warrior_placeholder.py

Remplacer un morceau par un vrai dessin : deposer le SVG dans skins/warrior/parts/ et retirer son
entree de PARTS ci-dessous pour que le script ne l'ecrase plus.
"""

import json
import math
import os
import re

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "humanoid")
SKELETON = os.path.join(ROOT, "humanoid.skeleton.json")
OUT_DIR = os.path.join(ROOT, "skins", "warrior", "parts")

# Repere du personnage : origine aux pieds, Y vers le bas.
VIEWBOX = (-60, -200, 160, 205)

OUTLINE = "#1e2228"
SKIN, SKIN_BACK = "#e0b48c", "#b88f6c"
STEEL, STEEL_BACK = "#9aa5b1", "#717b86"
CLOTH, CLOTH_BACK = "#9b2c2c", "#6f1f1f"
LEATHER, LEATHER_BACK = "#6b4428", "#4d301c"
PANTS, PANTS_BACK = "#3b3f4a", "#2a2d35"
GOLD = "#d4a640"
BLADE = "#dfe4ea"


def load_json_with_comments(path):
    with open(path, encoding="utf-8") as f:
        text = f.read()
    return json.loads(re.sub(r"^\s*//.*$", "", text, flags=re.MULTILINE))


def matrix(x, y, rotation, sx=1.0, sy=1.0):
    """Matrice affine (a, b, c, d, e, f) au sens SVG : echelle, puis rotation, puis translation."""
    r = math.radians(rotation)
    cos, sin = math.cos(r), math.sin(r)
    return (cos * sx, sin * sx, -sin * sy, cos * sy, x, y)


def multiply(child, parent):
    """Applique child puis parent (repere de l'os -> repere du parent -> monde)."""
    a1, b1, c1, d1, e1, f1 = child
    a2, b2, c2, d2, e2, f2 = parent
    return (
        a1 * a2 + b1 * c2, a1 * b2 + b1 * d2,
        c1 * a2 + d1 * c2, c1 * b2 + d1 * d2,
        e1 * a2 + f1 * c2 + e2, e1 * b2 + f1 * d2 + f2,
    )


def setup_world(skeleton):
    world = {}
    for bone in skeleton["bones"]:
        local = matrix(bone.get("x", 0), bone.get("y", 0), bone.get("rotation", 0),
                       bone.get("scaleX", 1), bone.get("scaleY", 1))
        parent = bone.get("parent")
        world[bone["name"]] = local if parent is None else multiply(local, world[parent])
    return world


# ── Formes, dans le repere de l'os : X le long de l'os ; pour le torse et la tete, +Y = l'avant ──

def capsule(x0, x1, half, fill):
    return (f'<rect x="{x0}" y="{-half}" width="{x1 - x0}" height="{2 * half}" rx="{half}" '
            f'fill="{fill}"/>')


def rect(x, y, w, h, fill, rx=0):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{fill}"/>'


def circle(cx, cy, r, fill):
    return f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="{fill}"/>'


def polygon(points, fill):
    pts = " ".join(f"{x},{y}" for x, y in points)
    return f'<polygon points="{pts}" fill="{fill}"/>'


def torso():
    return [
        polygon([(-16, -15), (4, -13), (4, 14), (-16, 17)], CLOTH),      # jupe de la tunique
        rect(-2, -14, 60, 29, CLOTH, rx=10),                              # tunique
        rect(18, -14, 38, 30, STEEL, rx=9),                               # plastron
        rect(0, -14.5, 8, 30, LEATHER),                                   # ceinture
        rect(1.5, 9, 5, 6, GOLD, rx=1),                                   # boucle
    ]


def head():
    return [
        capsule(-6, 8, 6, SKIN),                                          # cou
        circle(16, 2, 12, SKIN),                                          # visage
        polygon([(14, -14), (24, -14), (31, -6), (31, 8), (26, 13),       # casque
                 (17, 13), (17, 4), (12, 2), (8, -10)], STEEL),
        rect(4, 10, 14, 3, STEEL, rx=1.5),                                # nasal
        circle(17, 8.5, 1.6, OUTLINE),                                    # oeil
        rect(30, -3, 4, 6, GOLD, rx=1),                                   # cimier
    ]


def upperarm(front):
    shapes = [capsule(-6, 34, 7, CLOTH if front else CLOTH_BACK)]
    if front:
        shapes.append(circle(3, 0, 10.5, STEEL))                          # epaulette
    return shapes


def forearm(front):
    return [
        capsule(-5, 30, 6, SKIN if front else SKIN_BACK),
        rect(8, -6.5, 16, 13, LEATHER if front else LEATHER_BACK, rx=3),  # brassard
    ]


def hand(front):
    return [circle(5, 0, 6, LEATHER if front else LEATHER_BACK)]


def thigh(front):
    return [capsule(-8, 50, 9, PANTS if front else PANTS_BACK)]


def shin(front):
    return [
        capsule(-6, 48, 7.5, PANTS if front else PANTS_BACK),
        rect(20, -8.5, 30, 17, LEATHER if front else LEATHER_BACK, rx=4),  # botte
    ]


def foot(front):
    # Os du pied : pointe vers l'avant, +Y vers le sol ; la semelle touche y = 0 du personnage.
    return [rect(-8, -4, 28, 9, LEATHER if front else LEATHER_BACK, rx=4)]


def sword():
    return [
        polygon([(11, -4), (64, -4), (76, 0), (64, 4), (11, 4)], BLADE),  # lame
        f'<line x1="14" y1="0" x2="60" y2="0" stroke="{STEEL_BACK}" stroke-width="1.2"/>',
        rect(-9, -2.5, 18, 5, LEATHER, rx=2),                             # poignee
        rect(8, -10, 4, 20, GOLD, rx=1.5),                                # garde
        circle(-10, 0, 3.5, GOLD),                                        # pommeau
    ]


# Fichier -> (os, formes). Les noms de fichier sont ceux de warrior.skin.json.
PARTS = {
    "torso.svg": ("torso", torso()),
    "head.svg": ("head", head()),
    "upperarm_back.svg": ("upperarm_back", upperarm(False)),
    "forearm_back.svg": ("forearm_back", forearm(False)),
    "hand_back.svg": ("hand_back", hand(False)),
    "upperarm_front.svg": ("upperarm_front", upperarm(True)),
    "forearm_front.svg": ("forearm_front", forearm(True)),
    "hand_front.svg": ("hand_front", hand(True)),
    "thigh_back.svg": ("thigh_back", thigh(False)),
    "shin_back.svg": ("shin_back", shin(False)),
    "foot_back.svg": ("foot_back", foot(False)),
    "thigh_front.svg": ("thigh_front", thigh(True)),
    "shin_front.svg": ("shin_front", shin(True)),
    "foot_front.svg": ("foot_front", foot(True)),
    "sword.svg": ("weapon", sword()),
}


def write_part(name, bone, shapes, world):
    m = " ".join(f"{v:.4f}" for v in world[bone])
    vx, vy, vw, vh = VIEWBOX
    body = "\n    ".join(shapes)
    svg = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{vx} {vy} {vw} {vh}" '
           f'width="{vw}" height="{vh}">\n'
           f'  <!-- Os « {bone} ». Genere par assets/animation/generate_warrior_placeholder.py -->\n'
           f'  <g transform="matrix({m})" stroke="{OUTLINE}" stroke-width="1.5" '
           f'stroke-linejoin="round">\n    {body}\n  </g>\n</svg>\n')
    with open(os.path.join(OUT_DIR, name), "w", encoding="utf-8", newline="\n") as f:
        f.write(svg)


def main():
    world = setup_world(load_json_with_comments(SKELETON))
    os.makedirs(OUT_DIR, exist_ok=True)
    for name, (bone, shapes) in PARTS.items():
        write_part(name, bone, shapes, world)
    print(f"{len(PARTS)} SVG ecrits dans {OUT_DIR}")


if __name__ == "__main__":
    main()
