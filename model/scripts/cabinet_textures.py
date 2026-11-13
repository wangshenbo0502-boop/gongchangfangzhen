"""Create original cabinet PBR maps, engraved label atlas and HMI artwork."""
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parents[1] / "textures" / "electrical-cabinet"
LABELS = [
    ("asset", "PLC-01 / PROCESS CONTROL"),
    ("manufacturer", "WORKSHOP / ELECTRICAL SYSTEMS"),
    ("rating", "400 V AC / 50 Hz / 24 V DC"),
    ("serial", "EC-01 / 2026 / TRAINING MODEL"),
    ("warning", "DANGER / ELECTRICAL HAZARD"),
    ("disconnect", "ISOLATE SUPPLY BEFORE SERVICE"),
    ("hmi", "OPERATOR PANEL / HMI-01"),
    ("run", "RUN / SYSTEM READY"),
    ("alarm", "FAULT / ACKNOWLEDGE"),
    ("power", "CONTROL POWER / 24 V"),
    ("main", "Q0 / MAIN ISOLATOR"),
    ("estop", "EMERGENCY STOP"),
    ("start", "START / STOP"),
    ("auto", "AUTO / MANUAL"),
    ("q0", "Q0 / 3P / MAIN INCOMING"),
    ("q1", "Q1 / PUMP / C16"),
    ("q2", "Q2 / FAN / C10"),
    ("q3", "Q3 / AUX / C6"),
    ("q4", "Q4 / PLC / C6"),
    ("q5", "Q5 / HMI / C4"),
    ("q6", "Q6 / SPARE / C6"),
    ("k1", "K1 / PUMP CONTACTOR"),
    ("k2", "K2 / FAN CONTACTOR"),
    ("k3", "K3 / AUX CONTACTOR"),
    ("fr", "FR1 / OVERLOAD RELAY"),
    ("cpu", "CPU / PLC-01 / RUN"),
    ("io1", "DI 01 / 16 INPUT"),
    ("io2", "DI 02 / 16 INPUT"),
    ("io3", "DO 01 / 16 OUTPUT"),
    ("io4", "AO 01 / 4 OUTPUT"),
    ("psu", "PS1 / 24 V DC / 10 A"),
    ("switch", "SW1 / INDUSTRIAL ETH"),
    ("pe", "PE / PROTECTIVE EARTH"),
    ("x1", "X1 / FIELD TERMINALS"),
    ("fan", "FILTER / AIR INTAKE"),
    ("thermostat", "TH1 / 35 C"),
    ("duct", "CONTROL / SIGNAL WIRING"),
    ("inside", "SERVICE ACCESS / PLC-01"),
    ("ethernet", "ETH / PROFINET"),
    ("wire24", "+24V"),
    ("wire0", "0V"),
    ("wireL1", "L1"),
    ("wireL2", "L2"),
    ("wireL3", "L3"),
] + [(f"t{i:02}", f"{i:02} / X1") for i in range(1, 31)]


def font(size, bold=False):
    return ImageFont.truetype(
        "C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf", size
    )


def atlas():
    image = Image.new("RGB", (2048, 2048), (210, 215, 215))
    draw = ImageDraw.Draw(image)
    manifest = {}
    for index, (key, text) in enumerate(LABELS):
        x, y = (index % 4) * 512, (index // 4) * 128
        warning = key in ("warning", "disconnect", "estop")
        draw.rectangle((x, y, x+511, y+127),
                       fill=(234, 183, 34) if warning else (221, 226, 226))
        size = 33
        while draw.textbbox((0, 0), text, font=font(size, True))[2] > 475:
            size -= 1
        draw.text((x+256, y+64), text, font=font(size, True),
                  fill=(24, 32, 35), anchor="mm")
        manifest[key] = [x/2048, 1-(y+128)/2048, (x+512)/2048, 1-y/2048]
    image.save(OUT / "labels_BaseColor.png")
    (OUT / "labels.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")


def equipment_prints():
    specifications = {
        "mcb": (620, 250, (196, 199, 190)),
        "cpu": (1030, 850, (48, 57, 59)),
        "io": (450, 850, (48, 57, 59)),
        "contactor": (880, 550, (64, 70, 70)),
        "supply": (1250, 520, (191, 197, 194)),
        "rating": (1700, 670, (176, 181, 181)),
        "hazard": (720, 870, (224, 181, 33)),
        "drive": (1320, 670, (23, 47, 68)),
        "drive_display": (1050, 380, (28, 48, 39)),
        "network": (1600, 347, (27, 39, 44)),
        "safety": (440, 640, (55, 61, 59)),
        "operator": (1650, 165, (27, 36, 39)),
        "zones": (1680, 75, (27, 36, 39)),
    }
    for key, (w, h, background) in specifications.items():
        im = Image.new("RGB", (w, h), background)
        d = ImageDraw.Draw(im)
        fg = (220, 224, 220) if key in (
            "cpu", "io", "contactor", "drive", "network", "safety", "operator", "zones", "drive_display"
        ) else (22, 27, 29)
        if key == "operator":
            d.text((32, 22), "WORKSHOP", font=font(60, True), fill=fg)
            d.text((32, 102), "AUTOMATION  /  PLC-01", font=font(31), fill=(158, 171, 177))
            d.text((w-30, 54), "INDUSTRIAL TOUCH PANEL", font=font(34), fill=fg, anchor="ra")
            d.text((w-30, 112), "24 V DC     |     PROFINET", font=font(27), fill=(158, 171, 177), anchor="ra")
        elif key == "zones":
            d.text((36, 20), "RESET            ACK               CYCLE START          FEED HOLD                 OVERRIDE",
                   font=font(28), fill=(193, 203, 205))
        elif key == "drive":
            d.text((45, 25), "AC VECTOR DRIVE", font=font(66, True), fill=fg)
            d.text((45, 126), "VFD / 400 V 3~ / 7.5 kW", font=font(51), fill=fg)
            d.line((45, 220, w-45, 220), fill=(91, 117, 133), width=3)
            for yy, text in ((252, "INPUT   L1 / L2 / L3      PE"),
                             (334, "OUTPUT   U / V / W      PE"),
                             (416, "STO A / STO B     24 V DC"),
                             (515, "DC BUS  /  WAIT 5 MIN BEFORE SERVICE")):
                d.text((45, yy), text, font=font(38 if yy < 500 else 30), fill=fg)
        elif key == "drive_display":
            d.text((40, 27), "RUN     REMOTE / AUTO", font=font(42), fill=(157, 207, 171))
            d.text((40, 104), "50.00", font=font(155, True), fill=(183, 225, 188))
            d.text((w-45, 184), "Hz", font=font(72), fill=(157, 207, 171), anchor="ra")
            d.text((40, 300), "I  12.6 A       U  400 V       PID", font=font(37), fill=(157, 207, 171))
        elif key == "network":
            d.text((28, 21), "INDUSTRIAL ETHERNET", font=font(62, True), fill=fg)
            d.text((28, 112), "8 x RJ45  /  10-100-1000 BASE-T", font=font(44), fill=fg)
            d.text((28, 184), "MANAGED   |   RING   |   REDUNDANT 24 V DC", font=font(35), fill=(139, 169, 175))
            d.text((28, 272), "PWR 1     PWR 2     RUN     FAULT", font=font(32), fill=fg)
        elif key == "safety":
            d.text((26, 18), "SAFETY", font=font(61, True), fill=fg)
            for yy, text in ((126, "24 V DC"), (201, "E-STOP"), (276, "CH1 / CH2"), (352, "K1 / K2"),
                             (460, "13  23  33"), (530, "14  24  34")):
                d.text((26, yy), text, font=font(39), fill=fg)
        elif key == "mcb":
            d.text((24, 16), "C16", font=font(56, True), fill=fg)
            d.text((235, 22), "400 V ~", font=font(34), fill=fg)
            d.text((24, 92), "6000", font=font(29), fill=fg)
            d.rectangle((17, 89, 105, 128), outline=fg, width=2)
            d.text((159, 101), "3P    IEC 60898-1", font=font(29), fill=fg)
            d.line((38, 208, 38, 165, 97, 165, 133, 140), fill=fg, width=3)
            d.line((133, 173, 133, 208), fill=fg, width=3)
            d.text((230, 163), "I   ON      O   OFF", font=font(28), fill=fg)
        elif key in ("cpu", "io"):
            d.rectangle((0, 0, w, 64), fill=(24, 99, 86))
            d.text((24, 14), "CONTROL" if key == "cpu" else "I/O", font=font(32, True), fill=fg)
            d.text((24, 110), "CPU 1200" if key == "cpu" else "DI / DO", font=font(58, True), fill=fg)
            d.text((24, 194), "DC / DC / DC" if key == "cpu" else "24 V DC", font=font(29), fill=fg)
            if key == "cpu":
                for y, text in ((295, "RUN / STOP"), (359, "ERROR"), (423, "MAINT")):
                    d.text((36, y), text, font=font(30), fill=(163, 175, 172))
                d.text((30, 575), "DC INPUTS / RELAY OUTPUTS", font=font(30), fill=fg)
                d.text((30, 632), "X1  PROFINET (LAN)", font=font(30), fill=fg)
                d.text((30, 748), "PLC-01       6ES7 / TRAINING", font=font(28), fill=fg)
            else:
                for row in range(8):
                    d.text((24, 278+row*53), f"{row:02}        {row+8:02}",
                           font=font(27), fill=(175, 185, 178))
                d.text((24, 762), "16 CHANNEL", font=font(28), fill=fg)
        elif key == "contactor":
            d.text((35, 24), "K / MOTOR CONTROL", font=font(48, True), fill=fg)
            d.text((35, 103), "AC-3   400 V   18 A", font=font(41), fill=fg)
            d.text((35, 171), "24 V DC     A1 + / A2 -", font=font(32), fill=fg)
            for x in (90, 295, 500):
                d.line((x, 462, x, 350, x+42, 310), fill=fg, width=4)
                d.line((x+42, 352, x+42, 258), fill=fg, width=4)
            d.text((660, 311), "13 NO", font=font(28), fill=fg)
            d.text((660, 390), "14 NO", font=font(28), fill=fg)
        elif key == "supply":
            d.text((30, 20), "POWER SUPPLY", font=font(64, True), fill=fg)
            d.text((30, 122), "INPUT  100-240 V AC  50/60 Hz", font=font(44), fill=fg)
            d.text((30, 204), "OUTPUT  24 V DC  /  10 A", font=font(50, True), fill=fg)
            d.text((30, 316), "DC OK       ADJ. 22.5-28 V", font=font(37), fill=fg)
            d.text((30, 421), "L       N       PE          + V       - V", font=font(38), fill=fg)
        elif key == "rating":
            d.text((58, 25), "WORKSHOP  /  CONTROL SYSTEM", font=font(76, True), fill=fg)
            d.line((58, 130, w-58, 130), fill=fg, width=3)
            for y, text in ((165, "TYPE: EC-01         SERIAL: PLC-01-2026"),
                            (263, "400 V  3~   50 Hz      CONTROL: 24 V DC"),
                            (361, "DRAWING: EC01-EL-001     MASS: 165 kg"),
                            (459, "TRAINING MODEL / NOT A RATED PRODUCT")):
                d.text((58, y), text, font=font(43), fill=fg)
            for i in range(110):
                xx = 65+i*7
                d.rectangle((xx, 560, xx+(2 if i % 3 else 4), 620), fill=fg)
        else:
            d.polygon(((360, 70), (65, 590), (655, 590)), outline=fg, width=20)
            d.polygon(((383, 191), (255, 389), (345, 389),
                       (294, 514), (475, 313), (376, 313)), fill=fg)
            d.text((360, 690), "HIGH VOLTAGE", font=font(52, True), fill=fg, anchor="mm")
            d.text((360, 770), "ISOLATE BEFORE OPENING", font=font(31), fill=fg, anchor="mm")
        max_side = max(w, h)
        resized = im.resize((round(w/max_side*1024), round(h/max_side*1024)),
                            Image.Resampling.LANCZOS)
        square = Image.new("RGB", (1024, 1024), background)
        square.paste(resized, ((1024-resized.width)//2, (1024-resized.height)//2))
        square.save(OUT / (key+"_BaseColor.png"))


def legacy_hmi(fault=False):
    image = Image.new("RGB", (1024, 512), (151, 157, 158))
    d = ImageDraw.Draw(image)
    ink, light = (28, 37, 39), (222, 225, 222)
    d.rectangle((0, 0, 1024, 44), fill=(37, 57, 65))
    d.text((20, 12), "PLC-01   PROCESS CONTROL", font=font(22, True), fill=light)
    d.text((1004, 17), "AUTO   |   14:32:08", font=font(18), fill=light, anchor="ra")
    d.rectangle((10, 55, 728, 411), fill=(191, 197, 194), outline=(93, 101, 102), width=2)
    d.text((29, 69), "COOLING WATER CIRCUIT", font=font(20, True), fill=ink)
    d.line((115, 190, 115, 150, 281, 150, 281, 270, 455, 270, 455, 180, 620, 180),
           fill=(72, 91, 92), width=9)
    d.line((115, 190, 115, 150, 281, 150, 281, 270, 455, 270, 455, 180, 620, 180),
           fill=(37, 136, 94) if not fault else (122, 117, 96), width=4)
    d.rectangle((60, 189, 169, 348), fill=(207, 213, 210), outline=ink, width=3)
    d.rectangle((65, 232, 164, 343), fill=(81, 132, 149))
    d.text((115, 358), "TK-01", font=font(20, True), fill=ink, anchor="ma")
    d.text((115, 284), "72 %", font=font(22, True), fill=light, anchor="mm")
    d.ellipse((322, 236, 390, 304), fill=(162, 69, 49) if fault else (39, 130, 78),
              outline=ink, width=3)
    d.polygon(((337, 249), (337, 291), (377, 270)), outline=light, width=3)
    d.text((355, 318), "P-01", font=font(20, True), fill=ink, anchor="ma")
    d.text((355, 351), "0 rpm" if fault else "1450 rpm", font=font(21), fill=ink, anchor="ma")
    d.polygon(((430, 163), (480, 196), (480, 163), (430, 196)),
              fill=(45, 137, 84), outline=ink, width=2)
    d.text((455, 125), "V-02", font=font(19), fill=ink, anchor="ma")
    d.rectangle((578, 138, 679, 226), fill=(116, 126, 128), outline=ink, width=3)
    d.text((628, 181), "HX-01", font=font(20, True), fill=light, anchor="mm")
    d.text((627, 245), "32.4 C", font=font(22), fill=ink, anchor="ma")
    d.line((627, 226, 627, 382, 115, 382, 115, 348), fill=(74, 102, 124), width=7)
    d.rectangle((741, 55, 1012, 411), fill=(174, 181, 179), outline=(93, 101, 102), width=2)
    for y, tag, value in ((77, "MODE", "AUTOMATIC"), (145, "PUMP", "TRIP" if fault else "RUNNING"),
                           (213, "PRESSURE", "3.6 bar"), (281, "SUPPLY", "24.1 V DC"),
                           (349, "COMMUNICATION", "ONLINE")):
        d.text((754, y), tag, font=font(16), fill=(64, 76, 76))
        d.text((754, y+23), value, font=font(22, True), fill=ink)
    d.rectangle((0, 421, 1024, 452), fill=(158, 50, 35) if fault else (64, 90, 71))
    d.text((18, 427), "A012  MOTOR OVERLOAD / ACK REQUIRED" if fault else
           "NO ACTIVE ALARMS   /   SYSTEM ENABLED", font=font(19), fill=light)
    for i, text in enumerate(("OVERVIEW", "MANUAL", "TRENDS", "ALARMS", "SETTINGS")):
        x = i*205
        d.rectangle((x+4, 462, x+200, 507), fill=(111, 124, 127), outline=(73, 84, 87), width=2)
        d.text((x+102, 484), text, font=font(19, True), fill=light, anchor="mm")
    square = Image.new("RGB", (1024, 1024), (151, 157, 158))
    square.paste(image, (0, 256))
    square.save(OUT / ("hmi_fault_Emission.png" if fault else "hmi_Emission.png"))


def hmi(fault=False):
    # Full 16:9 screen; Chinese interface artwork is appropriate for this project.
    im = Image.new("RGB", (2048, 1152), (23, 30, 35))
    d = ImageDraw.Draw(im)
    cn_path = "C:/Windows/Fonts/msyh.ttc"
    def cn(n):
        return ImageFont.truetype(cn_path, n)
    ink, muted = (226, 234, 236), (141, 160, 170)
    green, cyan = (86, 196, 133), (65, 164, 197)
    d.rectangle((0, 0, 2048, 88), fill=(35, 44, 50))
    d.text((36, 21), "PLC-01", font=font(36, True), fill=ink)
    d.text((207, 22), "车间设备集中控制", font=cn(32), fill=ink)
    d.ellipse((1420, 32, 1435, 47), fill=green)
    d.text((1452, 27), "PLC ONLINE", font=font(25), fill=green)
    d.text((2008, 26), "2026-11-11   14:32:08", font=font(27), fill=muted, anchor="ra")
    d.rectangle((0, 89, 2048, 145), fill=(29, 38, 44))
    for x, text in ((38, "运行总览"), (253, "工艺流程"), (466, "设备诊断"), (681, "历史趋势"), (898, "报警记录")):
        d.text((x, 99), text, font=cn(26), fill=ink if x == 38 else muted)
    d.line((29, 142, 174, 142), fill=cyan, width=4)
    d.text((1998, 101), "自动模式  /  远程控制", font=cn(25), fill=green, anchor="ra")
    # Unframed mimic area uses simple shaded equipment, process tags and flow direction.
    d.text((38, 172), "循环水与通风系统", font=cn(29), fill=ink)
    d.text((38, 222), "P-01  /  TK-01  /  F-01", font=font(23), fill=muted)
    pipe_col = (75, 146, 163)
    path = (255, 542, 255, 650, 568, 650, 568, 552, 884, 552, 884, 330, 1086, 330)
    d.line(path, fill=(49, 72, 82), width=20, joint="curve")
    d.line(path, fill=pipe_col, width=8, joint="curve")
    d.line((1086, 440, 1086, 727, 255, 727, 255, 542), fill=(57, 91, 114), width=9)
    # Storage vessel with cylindrical highlight and gauge glass.
    d.rectangle((164, 336, 347, 548), fill=(110, 128, 136), outline=(165, 184, 192), width=3)
    for x in range(168, 344):
        v = int(81+52*(1-abs(x-234)/120))
        d.line((x, 344, x, 540), fill=(v, v+15, v+22))
    d.ellipse((164, 310, 347, 359), fill=(146, 163, 169), outline=(194, 206, 208), width=3)
    d.ellipse((164, 519, 347, 569), fill=(102, 124, 134), outline=(164, 184, 192), width=3)
    d.rectangle((322, 365, 332, 523), fill=(27, 40, 48), outline=muted, width=1)
    d.rectangle((325, 408, 329, 520), fill=cyan)
    d.text((255, 395), "72.4", font=font(45, True), fill=ink, anchor="ma")
    d.text((255, 453), "% LEVEL", font=font(23), fill=ink, anchor="ma")
    d.text((255, 584), "TK-01", font=font(29, True), fill=ink, anchor="ma")
    d.text((255, 620), "液位正常", font=cn(21), fill=green, anchor="ma")
    # End-suction pump: volute, shaft, ribbed motor, base.
    d.polygon(((456, 572), (703, 572), (737, 591), (485, 591)), fill=(49, 61, 68))
    d.ellipse((465, 488, 563, 581), fill=(47, 113, 133), outline=(111, 178, 192), width=3)
    d.ellipse((487, 511, 540, 561), fill=(81, 149, 165), outline=(169, 200, 205), width=2)
    d.rectangle((548, 516, 582, 546), fill=(166, 175, 175))
    d.rounded_rectangle((581, 493, 699, 564), 7, fill=(64, 119, 139), outline=(122, 171, 185), width=2)
    for x in range(591, 689, 10):
        d.line((x, 501, x, 557), fill=(32, 78, 100), width=4)
    d.ellipse((678, 497, 717, 559), fill=(39, 86, 109), outline=(119, 169, 183), width=2)
    d.rectangle((615, 479, 650, 497), fill=(90, 145, 162))
    d.text((591, 392), "P-01  循环泵", font=cn(26), fill=ink, anchor="ma")
    d.text((591, 433), "1450 rpm", font=font(31, True), fill=green if not fault else (222, 91, 78), anchor="ma")
    # Control valve and heat exchanger.
    d.polygon(((811, 529), (857, 575), (857, 529), (811, 575)), fill=(83, 159, 135), outline=ink, width=2)
    d.line((834, 522, 834, 500), fill=ink, width=4)
    d.rectangle((819, 489, 849, 501), fill=(125, 144, 150))
    d.text((834, 587), "V-02 / 68%", font=font(23), fill=muted, anchor="ma")
    d.rectangle((1007, 296, 1166, 445), fill=(80, 101, 114), outline=(156, 178, 186), width=3)
    for i in range(7):
        d.line((1023+i*20, 311, 1023+i*20, 430), fill=(139, 161, 169), width=5)
    d.text((1086, 253), "HX-01", font=font(28, True), fill=ink, anchor="ma")
    d.text((1086, 465), "32.4 °C", font=font(30, True), fill=ink, anchor="ma")
    for x, y in ((411, 650), (763, 552), (955, 330)):
        d.polygon(((x-10, y-8), (x+6, y), (x-10, y+8)), fill=(159, 210, 212))
    d.text((883, 656), "PT-01    3.60 bar", font=font(26), fill=cyan, anchor="ma")
    d.text((883, 691), "FT-01    42.8 m³/h", font=font(26), fill=cyan, anchor="ma")
    # Right side is a dense, conventional values table.
    d.line((1260, 172, 1260, 742), fill=(71, 85, 95), width=2)
    d.text((1300, 175), "实时参数", font=cn(29), fill=ink)
    table = (("循环泵频率", "50.00", "Hz"), ("电机电流", "12.6", "A"),
             ("母线电压", "565", "V DC"), ("柜内温度", "31.8", "°C"),
             ("排风机转速", "960", "rpm"), ("控制电源", "24.1", "V DC"))
    for i, (tag, value, unit) in enumerate(table):
        y = 240+i*77
        d.text((1300, y+13), tag, font=cn(25), fill=muted)
        d.text((1834, y), value, font=font(40, True), fill=ink, anchor="ra")
        d.text((1874, y+14), unit, font=font(23), fill=muted)
        d.line((1300, y+62, 2008, y+62), fill=(49, 62, 71), width=1)
    # One process trend strip, with labeled axes and a visible time scale.
    d.line((38, 774, 2008, 774), fill=(65, 80, 91), width=2)
    d.text((38, 796), "运行趋势 / 30 min", font=cn(25), fill=ink)
    d.text((510, 800), "— 压力    — 频率", font=cn(23), fill=cyan)
    for y in (859, 908, 957):
        d.line((115, y, 1983, y), fill=(45, 59, 69), width=1)
    for x in range(115, 1984, 267):
        d.line((x, 847, x, 971), fill=(45, 59, 69), width=1)
    pts = [(x, round(903+9*math.sin(x*.024)+3*math.sin(x*.081))) for x in range(115, 1984, 4)]
    d.line(pts, fill=cyan, width=3)
    d.line((115, 875, 1983, 875), fill=green, width=3)
    for i, text in enumerate(("14:00", "14:05", "14:10", "14:15", "14:20", "14:25", "14:30")):
        d.text((115+i*300, 979), text, font=font(20), fill=muted)
    d.rectangle((0, 1032, 2048, 1085), fill=(102, 41, 35) if fault else (29, 65, 49))
    d.text((36, 1042), "A012  电机过载 · 待确认" if fault else "系统运行正常 · 无活动报警",
           font=cn(24), fill=(236, 242, 234))
    d.text((2008, 1044), "PROFINET  OK    |    I/O  64/64    |    CYCLE  8 ms",
           font=font(24), fill=(205, 224, 216), anchor="ra")
    d.rectangle((0, 1086, 2048, 1152), fill=(39, 49, 56))
    for i, text in enumerate(("总览", "手动", "配方", "趋势", "报警", "设置")):
        x = 171+i*341
        d.text((x, 1101), text, font=cn(26), fill=ink if i == 0 else muted, anchor="ma")
    square = Image.new("RGB", (2048, 2048), (23, 30, 35))
    square.paste(im, (0, 448))
    square.save(OUT / ("hmi_fault_Emission.png" if fault else "hmi_Emission.png"))


def surfaces():
    rng = np.random.default_rng(82317)
    for family, size in (("brushed", 2048), ("zinc", 1024), ("polymer", 512)):
        a = rng.random((size, size), dtype=np.float32)-.5
        rows = rng.random((size, 1), dtype=np.float32)-.5
        yy, xx = np.mgrid[:size, :size].astype(np.float32)/size
        broad = np.sin(xx*17.3+np.sin(yy*6.2))*np.cos(yy*14.1+xx*4.2)
        if family == "brushed":
            height = .5+.035*rows+.008*a
            rough = .35+.065*rows+.018*broad+.012*a
            base = .94+.013*rows+.008*broad
        elif family == "zinc":
            facets = np.sin(xx*230+np.sin(yy*140)*1.7)*np.cos(yy*210)
            height = .5+.016*facets+.009*a
            rough = .36+.036*facets+.017*broad
            base = .94+.025*facets+.012*broad
        else:
            height = .5+.04*a
            rough = .55+.035*a+.012*broad
            base = .94+.009*broad
        dx = (np.roll(height, -1, 1)-np.roll(height, 1, 1))*.7
        dy = (np.roll(height, -1, 0)-np.roll(height, 1, 0))*.7
        channels = {
            "BaseColor": np.repeat(base[..., None], 3, axis=2),
            "Normal": np.stack((.5-dx, .5-dy, np.ones_like(dx)), axis=2),
            "Roughness": rough,
            "Metallic": np.ones_like(height) if family != "polymer" else np.zeros_like(height),
            "Height": height,
        }
        for channel, pixels in channels.items():
            Image.fromarray(np.uint8(np.clip(pixels, 0, 1)*255)).save(
                OUT / f"{family}_{channel}.png")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    surfaces()
    atlas()
    equipment_prints()
    hmi()
    hmi(True)
    print(f"CABINET_TEXTURES {OUT}")
