using System;
using UnityEngine;

namespace WorkshopDemo
{
    public enum FaultVisual { Heat, Vibration, Leak, Deformation, Electrical, Jam }
    public enum FaultSeverity { Warning, Critical }

    public sealed class WorkshopFault
    {
        public readonly string Code, Name, Mechanism, Symptom, Action;
        public readonly DeviceKind Kind;
        public readonly FaultVisual Visual;
        public readonly FaultSeverity Severity;
        public readonly bool Trips;
        public readonly float Temperature, Vibration, SpeedRatio, ProcessRatio;
        public string SeverityText => Severity == FaultSeverity.Critical ? "严重" : "预警";

        public WorkshopFault(string code, DeviceKind kind, string name, FaultVisual visual,
            bool trips, float temperature, float vibration, float speedRatio, float processRatio,
            string mechanism, string symptom, string action)
        {
            Code = code; Kind = kind; Name = name; Visual = visual; Trips = trips;
            Severity = trips ? FaultSeverity.Critical : FaultSeverity.Warning;
            Temperature = temperature; Vibration = vibration; SpeedRatio = speedRatio;
            ProcessRatio = processRatio; Mechanism = mechanism; Symptom = symptom; Action = action;
        }
    }

    public static class WorkshopFaultCatalog
    {
        // Teaching setpoints, not diagnostic limits or finite-element solutions.
        public static readonly WorkshopFault[] All = {
            new WorkshopFault("CV-JAM", DeviceKind.Conveyor, "异物卡阻", FaultVisual.Jam, true, 62, 0, 0, 1.6f,
                "异物阻塞导致驱动负载升高并触发联锁", "输送停止，物料滞留，负载升高",
                "演示：隔离动力、清除卡阻并检查联锁，复位试运行"),
            new WorkshopFault("CV-TRACK", DeviceKind.Conveyor, "输送跑偏", FaultVisual.Vibration, false, 46, 3.4f, .75f, 1.2f,
                "张紧不均或导向偏移引起侧向振动", "物料横向偏移，输送速度降低",
                "演示：停机检查导向与张紧，校正后复位"),
            new WorkshopFault("CV-BEAR", DeviceKind.Conveyor, "滚筒轴承损伤", FaultVisual.Heat, true, 83, 6.2f, 0, 1.4f,
                "轴承摩擦增大，温升与振动同时异常", "滚筒热点，保护停机",
                "演示：隔离动力，检查润滑与轴承，维护后复位"),
            new WorkshopFault("CV-SLIP", DeviceKind.Conveyor, "传动打滑", FaultVisual.Jam, false, 57, 2.2f, .4f, .7f,
                "传动摩擦不足使有效输送速度下降", "低速间歇输送，负载波动",
                "演示：检查传动、张紧及负载，调整后复位"),
            new WorkshopFault("M-OVER", DeviceKind.Motor, "过载过热", FaultVisual.Heat, true, 105, 2.8f, 0, 1.65f,
                "负载过大使绕组温升升高，热保护动作", "绕组区域发热，电机停转",
                "演示：隔离电源、检查负载与冷却，冷却后复位"),
            new WorkshopFault("M-BEAR", DeviceKind.Motor, "轴承磨损", FaultVisual.Vibration, false, 72, 6.5f, .9f, 1.15f,
                "轴承间隙与摩擦增大产生机械振动", "轴端振动增大，温度升高",
                "演示：停机检查轴承间隙、润滑与对中，维护后复位"),
            new WorkshopFault("M-PHASE", DeviceKind.Motor, "缺相保护", FaultVisual.Electrical, true, 88, 3.6f, 0, 1.8f,
                "供电缺相引起电流不平衡与保护动作", "电气故障闪烁，保护停机",
                "演示：隔离电源、检查三相供电与接线，确认后复位"),
            new WorkshopFault("M-ALIGN", DeviceKind.Motor, "联轴器不对中", FaultVisual.Vibration, false, 61, 4.8f, .85f, 1.25f,
                "轴线偏差使联轴器承受附加载荷", "周期摆振，传动效率降低",
                "演示：停机检查联轴器与地脚，完成对中后复位"),
            new WorkshopFault("P-HOT", DeviceKind.Pump, "超温保护", FaultVisual.Heat, true, 92, 5.8f, 0, 1.25f,
                "冷却或润滑异常导致泵体温升与保护停机", "泵体热点，转速归零",
                "演示：完成冷却、润滑检查并复位"),
            new WorkshopFault("P-CAV", DeviceKind.Pump, "汽蚀", FaultVisual.Vibration, false, 66, 6.1f, .85f, .5f,
                "入口供液不足或压力降低导致汽泡形成与溃灭", "高频振动，出口压力与流量下降",
                "演示：停泵检查入口供液、过滤器与吸入条件，恢复后复位"),
            new WorkshopFault("P-SEAL", DeviceKind.Pump, "机械密封泄漏", FaultVisual.Leak, true, 58, 2.6f, 0, .4f,
                "密封副磨损或损坏导致介质外泄", "密封位置可见漏液，保护停机",
                "演示：停泵隔离介质，检查并维护密封，试漏后复位"),
            new WorkshopFault("P-BEAR", DeviceKind.Pump, "轴承损伤", FaultVisual.Vibration, false, 76, 7.2f, .8f, .75f,
                "轴承损伤使机械振动与摩擦温升增加", "轴端摆振，出力下降",
                "演示：停泵检查轴承、润滑和对中，维护后复位"),
            new WorkshopFault("TK-PRESS", DeviceKind.Tank, "超压联锁", FaultVisual.Deformation, true, 62, 0, 0, 1.9f,
                "调压失效或出口堵塞导致容器压力升高", "压力升高，轮廓脉动表示承压异常",
                "演示：隔离进料、按流程泄压，检查调压与联锁后复位"),
            new WorkshopFault("TK-LEAK", DeviceKind.Tank, "罐体泄漏", FaultVisual.Leak, true, 38, 0, 0, .3f,
                "局部密封或壁面缺陷导致介质流失", "罐底可见漏液，液位下降",
                "演示：隔离介质、控制泄漏，检查密封与壁面后复位"),
            new WorkshopFault("TK-LEVEL", DeviceKind.Tank, "高液位预警", FaultVisual.Jam, false, 40, 0, 0, 1.1f,
                "进出料失衡使液位接近演示上限", "液位升高，联锁指示闪烁",
                "演示：核对液位信号与阀门，调整进出料后复位"),
            new WorkshopFault("TK-SUPPORT", DeviceKind.Tank, "支座变形", FaultVisual.Deformation, true, 41, 0, 0, 1.15f,
                "支撑刚度下降或不均匀沉降引起附加载荷", "支座异常轮廓；形变仅作夸张示意",
                "演示：停用并隔离，检查支座、基础和结构，确认后复位"),
            new WorkshopFault("F-IMBAL", DeviceKind.Fan, "叶轮不平衡", FaultVisual.Vibration, false, 61, 7.0f, .8f, .7f,
                "叶轮积灰或质量偏心引起周期离心载荷", "叶轮摆振，风量下降",
                "演示：断电检查积灰与叶片，清洁并校平衡后复位"),
            new WorkshopFault("F-BLOCK", DeviceKind.Fan, "风道堵塞", FaultVisual.Jam, false, 74, 3.2f, .65f, .3f,
                "风道阻力增大导致通风量降低", "转动迟缓，风量显著下降",
                "演示：断电清理风道与滤网，检查风阀后复位"),
            new WorkshopFault("F-BEAR", DeviceKind.Fan, "轴承抱死", FaultVisual.Heat, true, 98, 7.8f, 0, 0,
                "润滑失效或轴承损坏使转子锁止", "轴承热点，风机停转",
                "演示：隔离电源、检查轴承与润滑，维护后复位"),
            new WorkshopFault("F-CRACK", DeviceKind.Fan, "叶片裂纹风险", FaultVisual.Deformation, true, 52, 8.4f, 0, 0,
                "叶片局部刚度降低，存在疲劳失效风险", "叶片警示轮廓，联锁停机",
                "演示：停机隔离，检查叶片与连接，完成专业检查后复位"),
            new WorkshopFault("PLC-HOT", DeviceKind.Cabinet, "柜内散热失效", FaultVisual.Heat, true, 79, 0, 0, 1,
                "滤网堵塞或散热风扇失效使柜内温度升高", "柜体热点，控制输出联锁",
                "演示：隔离电源、检查风扇与滤网，冷却后复位"),
            new WorkshopFault("PLC-LOW", DeviceKind.Cabinet, "控制电源欠压", FaultVisual.Electrical, true, 39, 0, 0, .72f,
                "控制电源供电异常导致电压不足", "24 V 电源跌落，控制输出停止",
                "演示：隔离电源、核对供电与负载，恢复后复位"),
            new WorkshopFault("PLC-COMM", DeviceKind.Cabinet, "现场总线中断", FaultVisual.Electrical, false, 36, 0, 1, 1,
                "现场总线断连导致设备数据质量下降", "通信质量为 0%，柜内电源仍正常",
                "演示：检查通信供电、线缆和网络，恢复通信后复位"),
            new WorkshopFault("PLC-CONTACT", DeviceKind.Cabinet, "接点过热", FaultVisual.Heat, true, 96, 0, 0, 1.2f,
                "接点松动或接触电阻增大产生局部发热", "端子区热点，控制回路联锁",
                "演示：隔离验电后检查接点，维护并复核后复位")
        };

        public static WorkshopFault[] For(DeviceKind kind) => Array.FindAll(All, f => f.Kind == kind);
        public static WorkshopFault Find(string code) => Array.Find(All, f => f.Code == code);
        public static Color ColorFor(FaultVisual visual) =>
            visual == FaultVisual.Leak ? new Color(.12f, .7f, 1) :
            visual == FaultVisual.Vibration ? new Color(1, .68f, .12f) :
            visual == FaultVisual.Deformation ? new Color(1, .3f, .58f) :
            visual == FaultVisual.Electrical ? new Color(.8f, .7f, 1) : new Color(1, .25f, .08f);
    }
}
