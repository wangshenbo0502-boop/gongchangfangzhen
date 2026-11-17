using System.Collections.Generic;
using UnityEngine;

namespace WorkshopDemo
{
    // Parameters are explicit teaching assumptions, not measured limits or solver output.
    internal sealed class WorkshopFaultCase
    {
        public readonly string Code, Binding, Component, Evidence, Repair, Retest;
        public readonly string MetricKey, MetricLabel, Unit;
        public readonly float? Normal, Abnormal;
        public readonly string Effect;
        public readonly WorkshopFaultProfile Profile;

        WorkshopFaultCase(string code, string binding, string component, string effect,
            string key, string label, string unit, float? normal, float? abnormal,
            string evidence, string repair, string retest)
        {
            Code = code; Binding = binding; Component = component; Effect = effect;
            MetricKey = key; MetricLabel = label; Unit = unit; Normal = normal; Abnormal = abnormal;
            Evidence = evidence; Repair = repair; Retest = retest;
            Profile = new WorkshopFaultProfile(code, binding, component, "验收版原设备 · 局部教学示意",
                evidence, "异常定位", .25f, "异常发展 / 保护策略", .7f);
        }

        static WorkshopFaultCase C(string code, string binding, string component, string effect,
            string key, string label, string unit, float? normal, float? abnormal,
            string evidence, string repair, string retest) =>
            new WorkshopFaultCase(code, binding, component, effect, key, label, unit, normal, abnormal,
                evidence, repair, retest);

        public static readonly WorkshopFaultCase[] All = {
            C("P-HOT","pump.bearing.housing","泵轴承座","thermal","temperature","轴承座温度","°C",40,92,
                "轴承座局部温升与停机","检查冷却与润滑","冷却后核对温度趋势及保护"),
            C("P-CAV","pump.inlet.cover","吸入口与叶轮入口","cavitation","flow","出口流量","m³/h",18,9,
                "入口条件与出口压力流量波动","恢复供液并检查入口条件","同转速压力与流量稳定"),
            C("P-SEAL","pump.seal.chamber","机械密封副与轴侧出口","leak","leak_rate","漏率","L/min",null,null,
                "密封出口湿润和滴漏","隔离后维护密封副","复装试漏并核对静动环"),
            C("P-BEAR","pump.bearing.housing","泵轴承座","orbit","vibration","轴承振动","mm/s",1.2f,7.2f,
                "外圈滚道局部预设最多 1 mm 材料损失；非接触求解","检查润滑、间隙和对中","同转速振动恢复并检查温升"),
            C("TK-PRESS","tank.pressure","顶部压力表","pressure","pressure","罐压","MPa",.45f,.855f,
                "压力表异常与进料联锁","隔离进料并按规程泄压","核对压力和联锁恢复"),
            C("TK-LEAK","tank.shell.frontLeak","巡检侧筒体下段","leak","level","液位","%",65,38,
                "筒壁漏点及液位趋势","隔离介质并维护局部壁面","试漏及液位趋势复核"),
            C("TK-LEVEL","tank.instruments.level","液位柱与进出料","level","level","液位","%",65,96,
                "液位柱上升与进出料失衡","核对阀位及进出料","液位稳定且高液位信号解除"),
            C("TK-SUPPORT","tank.support","前侧支腿底板","offset","settlement","支撑偏差","mm",0,1.5f,
                "局部底板偏差；教学几何非测量","隔离并检查基础与支座","复核支座与基础水平"),
            C("F-IMBAL","fan.impeller.hub","叶轮轮毂与支撑","orbit","vibration","同转速振动","mm/s",1.2f,7,
                "局部轴心轨迹与同转速振动对照","清洁叶轮并校平衡","同转速振动与紧固复核"),
            C("F-BLOCK","fan.inlet","进风护网","restriction","airflow","相对风量","%",100,30,
                "入口受阻位置与风量下降","清理风道滤网并检查风阀","同转速风量恢复"),
            C("F-BEAR","fan.motor.bearing","电机轴承端盖","thermal","temperature","轴承温度","°C",40,98,
                "轴承端盖热点与停转；叶片不变形","检查轴承与润滑","手盘及温升复核"),
            C("F-CRACK","fan.impeller.crack","第一叶片局部前缘","crack","crack_length","裂纹长度","mm",null,null,
                "预制局部裂面；后缘保持连接","隔离并完成叶片专业检查","确认检查结论和剩余连接"),
            C("PLC-HOT","cabinet.cooling","柜内散热滤网","thermal","temperature","柜内温度","°C",35,79,
                "散热通道受阻与柜内温升","维护风扇及滤网","散热运行与柜温复核"),
            C("PLC-LOW","cabinet.supply","24 V 控制电源","signal","voltage","控制电压","V",24,17.3f,
                "电源测点欠压与控制输出停止","检查供电和负载","电压及控制输出复测"),
            C("PLC-COMM","cabinet.network","工业网络节点","signal","communication","通信质量","%",100,0,
                "网络质量丢失；控制电源保持","检查网络供电线缆与连接","通信恢复且电源正常"),
            C("PLC-CONTACT","cabinet.fieldTerminal.candidate","X1-01 带载端子夹持件","thermal","temperature","端子温度","°C",25,96,
                "X1-01 局部热点和隔离后冷却","隔离验电并检查接点紧固","同负载端子温度复测"),
            C("P-ALIGN","pump.coupling","弹性联轴器","orbit","alignment","教学轴线偏差","mm",0,.35f,
                "联轴器偏心及同转速振动","检查软脚并对中","对中记录及同转速振动复核"),
            C("P-INLET","pump.inlet.cover","入口盖与供液通路","restriction","flow","出口流量","m³/h",18,6.3f,
                "原入口内新增教学滤网及受阻物；非原设备实物或流阻求解","清洗过滤器并核对阀位","供液条件和同转速流量复测"),
            C("TK-CORROSION","tank.shell.frontLeak","巡检侧局部罐壁","corrosion","wall_thickness","剩余壁厚","mm",null,null,
                "预设局部减薄；实际壁厚须测厚提供","定点测厚并评估剩余承载","专业评估与测厚记录"),
            C("TK-FLANGE","tank.outlet.flange","出口法兰接合面","leak","leak_rate","漏率","L/min",null,null,
                "出口法兰接合面湿润","隔离泄压并检查垫片","复装试漏记录"),
            C("F-LOOSE","fan.support","电机支撑紧固点","orbit","vibration","连接振动","mm/s",1.2f,5.5f,
                "支撑局部相对运动","检查并紧固支架连接","紧固记录与同转速复测"),
            C("F-RUB","fan.impeller.crack","叶尖与风筒间隙","wear","clearance","叶尖间隙","mm",null,null,
                "叶尖擦碰位置；间隙未测量","排除偏移并检查叶尖","手盘无擦碰及间隙记录"),
            C("PLC-OVERLOAD","cabinet.breaker","受控支路断路器","signal","branch_load","支路负载","%",65,104,
                "支路负载升高与断路器动作","核对负载并排除过载","负载与保护动作复测"),
            C("PLC-SENSOR","cabinet.plc","PLC 模拟量输入","signal","input_signal","模拟量输入","mA",12,16,
                "输入信号偏离 12 mA 教学参考点","校验传感器及输入量程","与独立参考点比对"),
            C("P-IMPELLER","pump.impeller.inlet","第一叶轮叶片表面","wear","flow","出口流量","m³/h",18,11.7f,
                "第一叶片外缘预设最多 2 mm 磨蚀；非冲蚀求解","检查修复叶轮并校平衡","同转速出力及平衡复核"),
            C("P-DRY","pump.seal.chamber","机械密封与入口供液","thermal","temperature","密封温度","°C",40,104,
                "供液丢失与密封摩擦升温","恢复灌泵条件并检查密封","供液、试漏及温升复核"),
            C("TK-SENSOR","tank.instruments.level","液位变送器与独立液位柱","signal","indicated_level","仪表示值","%",65,91,
                "仪表示值与 65% 教学独立液位不一致","检查信号并校验变送器","两种液位读数比对"),
            C("TK-VENT","tank.vent","顶部呼吸通路","restriction","pressure","罐压","MPa",.45f,.6075f,
                "呼吸通路受阻与进出料压差","隔离泄压后检查呼吸通路","呼吸阀及压力恢复复核"),
            C("F-BLADE","fan.impeller.crack","第一叶片外缘磨蚀区","erosion","airflow","相对风量","%",100,60,
                "既有边缘磨蚀与风量损失；非磨蚀速率预测","检查叶缘并专业修复及校平衡","叶缘检查记录及同转速风量"),
            C("F-REVERSE","fan.impeller.hub","叶轮转向","reverse","rotation_direction","转向符号","",1,-1,
                "转向与正常对照相反","核对驱动方向及接线","点动转向与风量复测"),
            C("PLC-IO","cabinet.plc","PLC 数字量输出","signal","output_feedback","输出反馈","%",100,0,
                "100% 教学命令与输出反馈不一致","检查输出模块及回路","逐点命令反馈复测"),
            C("PLC-DRIVE","cabinet.drive","变频驱动器","signal","output_frequency","输出频率","Hz",50,0,
                "驱动保护反馈与输出频率归零","核对驱动记录并排除负载异常","受控复位及输出频率复测")
        };

        public static WorkshopFaultCase Find(string code) => System.Array.Find(All, c => c.Code == code);

        public IEnumerable<WorkshopFaultReadout> Readouts(WorkshopFaultFrame f)
        {
            float severity = Mathf.SmoothStep(0, 1, f.Progress / .7f);
            float? value = Normal.HasValue && Abnormal.HasValue ? Mathf.Lerp(Normal.Value, Abnormal.Value, severity) : null;
            if (Code == "F-REVERSE") value = f.Progress <= 0 ? 1 : -1;
            if (MetricKey == "temperature") value = f.Temperature;
            if (MetricKey == "flow") value = f.Flow;
            if (MetricKey == "pressure") value = f.Pressure;
            if (MetricKey == "level") value = f.Level;
            if (MetricKey == "vibration") value = f.Vibration;
            if (MetricKey == "voltage") value = f.Voltage;
            if (MetricKey == "communication") value = f.Communication;
            yield return new WorkshopFaultReadout(MetricKey, MetricLabel, value, Unit, value.HasValue ? f.TelemetrySource : "未提供 / 未求解");
            if (Code == "TK-CORROSION")
            {
                yield return new WorkshopFaultReadout("nominal_wall", "名义完好壁厚", 8, "mm", "展示模型名义几何");
                yield return new WorkshopFaultReadout("preset_wall", "中心预设剩余壁厚",
                    f.Progress >= .25f ? 4 : 8, "mm", "教学几何预设，非测厚或求解；不预测腐蚀速率");
            }
            if (Normal.HasValue)
                yield return new WorkshopFaultReadout(MetricKey + "_baseline", "同条件完好参考", Normal, Unit, "教学参考设定");
            if (Code == "PLC-SENSOR") yield return new WorkshopFaultReadout("reference_signal", "独立参考信号", 12, "mA", "教学参考设定");
            if (Code == "PLC-IO") yield return new WorkshopFaultReadout("output_command", "输出命令", 100, "%", "教学参考设定");
            if (Code == "TK-SENSOR") yield return new WorkshopFaultReadout("reference_level", "独立液位", 65, "%", "教学参考设定");
            if (Code == "PLC-COMM" || Code == "PLC-OVERLOAD")
                yield return new WorkshopFaultReadout("voltage", "控制电源", f.Voltage, "V", f.TelemetrySource);
            if (Code == "F-IMBAL" || Code == "P-BEAR" || Code == "P-ALIGN" || Code == "F-LOOSE")
                yield return new WorkshopFaultReadout("rpm", "对照转速", f.Rpm, "r/min", f.TelemetrySource);
            if (Code == "F-IMBAL")
                yield return new WorkshopFaultReadout("rotation_time_scale", "旋转时间倍率",
                    WorkshopFaultProfile.ImbalanceRotationScale, "×", "教学慢放；轨迹跟随转子相位，不代表实际振动频率");
        }
    }
}
