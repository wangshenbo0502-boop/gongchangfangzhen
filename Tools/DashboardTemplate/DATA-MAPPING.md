# device_monitor 数据填写说明

> 对应源模板：**064 设备环境监测平台**
> 渲染骨架：`assets/templates/skeletons/device_monitor/`（（含 injector.js + template_config.json + 内置源，入口 index.html，免 --source-dir））

## 顶层字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `title` | string | 大屏标题 |
| `statCards` | array[3] | 左侧统计卡（在线/告警/离线设备），每项 `{label, value}` |
| `navItems` | array[3] | 中间导航数字（今日工单/处理中/已完成），每项 `{label, value}` |
| `switchCount` | object | 开关次数 `{value}` |
| `environment` | array[4] | 环境指标（温度/湿度/信号/光线），每项 `{label, value}`，value 为带单位字符串 |
| `table` | object | 维护记录表 `{rows[]}`，每行 4 列（区域/状态/温度/湿度） |
| `charts.pie` | object | 设备状态饼图 `{data[{name,value}]}` |
| `charts.barLine` | object | 检测统计 `{xAxis[], yAxis[][], lines[][], title}` |
| `charts.map` | object | 广西地图 `{data[{name,value}]}`，name 须命中 geoCoordMap 城市名 |
| `charts.hbar` | object | 设备使用频率横向柱 `{names[], values[], valdata[]}` |

## 字段 -> 渲染目标映射

| 字段 | 渲染目标 | 类型 |
|---|---|---|
| `title` | DOM `.t_header span` | text |
| `statCards[0]` | DOM `.t_mbox.t_rbox` | text |
| `statCards[1]` | DOM `.t_mbox.t_gbox` | text |
| `statCards[2]` | DOM `.t_mbox.t_ybox` | text |
| `navItems[0..2]` | DOM `.t_nav li:nth-child(N)` | text |
| `switchCount.value` | DOM `.t_b_h h3`（追加 `次`） | html |
| `environment[0..3]` | DOM `.t_b_box / .t_b_box1 / .t_b_box2 / .t_b_box3` | text |
| `table.rows` | DOM `.t_table tbody` | table |
| `charts.pie.data` | echarts `#chart_1` (pie) | pie |
| `charts.barLine` | echarts `#chart_2` (bar+line) | bar+line |
| `charts.map.data` | echarts `#chart_3` (map+effectScatter) | map |
| `charts.hbar` | echarts `#chart_4` (horizontalBar) | bar |

## 填写注意事项

- `statCards`/`navItems`/`environment` 必须按数组顺序填，长度分别 ≥3/3/4
- `environment` 的 `value` 是带单位的字符串（如 `"26.5℃"`、`"58%"`、`"-67dBm"`、`"420lux"`），由用户拼好
- `table.rows` 每行是字符串数组，列数需与表头一致（4 列）
- `charts.map.data` 的 `name` 必须命中模板 `js.js` 内 geoCoordMap 的城市名（南宁/柳州/桂林/梧州/北海 等），否则散点不显示
- `charts.barLine.yAxis` 与 `lines` 都是二维数组（外层数组对应 series），injector 取 `[0]`
- `charts.hbar.valdata` 通常填全 100（用作灰色背景柱）

## 构建命令

```bash
python scripts/build_dashboard.py assets/templates/data/device_monitor/example.json \
    --template device_monitor \
    --source-dir "<BigDataView 源目录>/web" \
    --output dashboard.html
```
