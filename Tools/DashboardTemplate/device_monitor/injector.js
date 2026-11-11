/**
 * DASHBOARD_DATA 运行时注入器
 * 模板: 064 设备环境监测平台
 * 
 * 在页面渲染完成后,读取 window.DASHBOARD_DATA 并:
 * 1. 更新所有 DOM 文本节点(统计卡/导航/环境指标/表格)
 * 2. 通过 echarts.getInstanceByDom 获取图表实例并 setOption 更新数据
 */
$(function() {
  // 延迟执行,确保所有 ECharts 图表已初始化
  setTimeout(function() {
    if (!window.DASHBOARD_DATA) {
      console.warn('[Dashboard Injector] DASHBOARD_DATA not found, using default demo data');
      return;
    }

    var D = window.DASHBOARD_DATA;

    // ====== 1. DOM 文本更新 ======

    // 标题
    if (D.title) $('.t_header span').text(D.title);

    // 左侧统计卡片
    if (D.statCards && D.statCards.length >= 3) {
      var cards = D.statCards;
      $('.t_mbox.t_rbox span').text(cards[0].label || '');
      $('.t_mbox.t_rbox h2').text(cards[0].value || '');
      $('.t_mbox.t_gbox span').text(cards[1].label || '');
      $('.t_mbox.t_gbox h2').text(cards[1].value || '');
      $('.t_mbox.t_ybox span').text(cards[2].label || '');
      $('.t_mbox.t_ybox h2').text(cards[2].value || '');
    }

    // 中间导航项
    if (D.navItems && D.navItems.length >= 3) {
      var nav = D.navItems;
      $('.t_nav li:nth-child(1) span').text(nav[0].label || '');
      $('.t_nav li:nth-child(1) h1').text(nav[0].value || '');
      $('.t_nav li:nth-child(2) span').text(nav[1].label || '');
      $('.t_nav li:nth-child(2) h1').text(nav[1].value || '');
      $('.t_nav li:nth-child(3) span').text(nav[2].label || '');
      $('.t_nav li:nth-child(3) h1').text(nav[2].value || '');
    }

    // 开关次数
    if (D.switchCount != null) {
      var sc = D.switchCount;
      $('.t_b_h h3').html((sc.value || sc) + '<span>次</span>');
    }

    // 环境指标(温度/湿度/信号/光线)
    if (D.environment && D.environment.length >= 4) {
      var env = D.environment;
      $('.t_b_box span').text(env[0].label || '溫度');
      $('.t_b_box h2').text(env[0].value || '');
      $('.t_b_box1 span').text(env[1].label || '湿度');
      $('.t_b_box1 h2').text(env[1].value || '');
      $('.t_b_box2 span').text(env[2].label || '信号');
      $('.t_b_box2 h2').text(env[2].value || '');
      $('.t_b_box3 span').text(env[3].label || '光线');
      $('.t_b_box3 h2').text(env[3].value || '');
    }

    // 表格
    if (D.table && D.table.rows && D.table.rows.length > 0) {
      var tbody = $('.t_table tbody');
      tbody.empty();
      D.table.rows.forEach(function(row) {
        var tr = '<tr>';
        (row.cells || row).forEach(function(cell) {
          tr += '<td>' + (cell || '') + '</td>';
        });
        tr += '</tr>';
        tbody.append(tr);
      });
    }

    // ====== 2. 图表数据更新 ======
    if (!D.charts) return;

    // chart_1: 饼图 - 设备状态统计
    if (D.charts.pie && D.charts.pie.data) {
      var pieChart = echarts.getInstanceByDom(document.getElementById('chart_1'));
      if (pieChart) {
        pieChart.setOption({
          series: [{
            data: D.charts.pie.data.map(function(d) {
              return { value: d.value, name: d.name };
            })
          }],
          legend: {
            data: D.charts.pie.data.map(function(d) { return d.name; })
          }
        });
      }
    }

    // chart_2: 柱状+折线 - 检测统计
    if (D.charts.barLine) {
      var bl = D.charts.barLine;
      var ch2 = echarts.getInstanceByDom(document.getElementById('chart_2'));
      if (ch2 && bl.xAxis && bl.yAxis && bl.lines) {
        ch2.setOption({
          xAxis: { data: bl.xAxis },
          series: [
            { data: bl.yAxis[0] || [] },
            { data: bl.lines[0] || [] }
          ]
        });
        if (bl.title) {
          ch2.setOption({ title: [{ text: bl.title }] });
        }
      }
    }

    // chart_3: 地图 - 设备分布
    // 注：地图使用 guangxi.js 注册的自定义 geo，坐标映射在原始 JS 闭包内。
    // 此处用 setOption 增量更新散点数据，geoCoordMap 内的城市名必须匹配。
    if (D.charts.map && D.charts.map.data) {
      try {
        var mapChart = echarts.getInstanceByDom(document.getElementById('chart_3'));
        if (mapChart) {
          var mapData = D.charts.map.data;
          var curOpt = mapChart.getOption();
          // 仅更新 series[0] (light map) 和 series[3] (effectScatter) 的 name/value 数据
          // 原始 convertData 使用闭包内的 geoCoordMap，不可从外部访问
          var newMapData = mapData.map(function(d) { return { name: d.name, value: d.value }; });
          if (curOpt.series && curOpt.series.length >= 4) {
            mapChart.setOption({
              series: [
                { data: newMapData },
                {}, // scatter 散点保持
                {}, // light map 保持
                { data: newMapData.slice(0, 5) } // effectScatter Top5
              ]
            });
          }
        }
      } catch(e) {
        console.warn('[Injector] Map chart update skipped:', e.message);
      }
    }

    // chart_4: 横向柱状图 - 设备使用频率
    if (D.charts.hbar) {
      var hb = D.charts.hbar;
      var ch4 = echarts.getInstanceByDom(document.getElementById('chart_4'));
      if (ch4 && hb.names && hb.values) {
        ch4.setOption({
          yAxis: [{ data: hb.names }, { data: hb.valdata || [] }],
          series: [
            { data: hb.values },
            { data: (hb.names || []).map(function() { return 100; }) }
          ]
        });
      }
    }

  }, 300);
});
