window.DEFAULT_CAMPAIGN = {
  "schemaVersion": 1,
  "id": "astra_first_voyage",
  "name": "探测任务 04 · 深空航程",
  "startSectorId": "sector_1",
  "resources": [
    {
      "id": "fuel",
      "name": "燃料",
      "initial": 10,
      "capacity": 24
    },
    {
      "id": "supplies",
      "name": "补给",
      "initial": 18,
      "capacity": 30
    },
    {
      "id": "scrap",
      "name": "材料",
      "initial": 8,
      "capacity": 99
    },
    {
      "id": "data",
      "name": "数据",
      "initial": 0,
      "capacity": 99
    }
  ],
  "travelCosts": [
    {
      "resourceId": "fuel",
      "amount": 1
    },
    {
      "resourceId": "supplies",
      "amount": 1
    }
  ],
  "sectors": [
    {
      "id": "sector_1",
      "name": "边缘观测带",
      "description": "探测舱航路 / 请选择一条分支并向右侧出口前进。",
      "startNodeId": "entry",
      "nextSectorId": "sector_2",
      "nodes": [
        {
          "id": "entry",
          "name": "跃迁入口",
          "x": 0,
          "y": 0.5,
          "eventId": "",
          "next": [
            "signal_a",
            "signal_b",
            "signal_c"
          ],
          "exit": false
        },
        {
          "id": "signal_a",
          "name": "星球解体",
          "x": 0.25,
          "y": 0.08,
          "eventId": "show_1",
          "next": [
            "salvage",
            "trade"
          ],
          "exit": false
        },
        {
          "id": "signal_b",
          "name": "陌生访客",
          "x": 0.25,
          "y": 0.5,
          "eventId": "show_2",
          "next": [
            "salvage",
            "trade"
          ],
          "exit": false
        },
        {
          "id": "signal_c",
          "name": "舱体撞击",
          "x": 0.25,
          "y": 0.92,
          "eventId": "show_3",
          "next": [
            "salvage",
            "trade"
          ],
          "exit": false
        },
        {
          "id": "salvage",
          "name": "漂流物资",
          "x": 0.5,
          "y": 0.23,
          "eventId": "salvage",
          "next": [
            "relay"
          ],
          "exit": false
        },
        {
          "id": "trade",
          "name": "补给信标",
          "x": 0.5,
          "y": 0.77,
          "eventId": "trade",
          "next": [
            "relay"
          ],
          "exit": false
        },
        {
          "id": "relay",
          "name": "数据中继",
          "x": 0.76,
          "y": 0.5,
          "eventId": "relay",
          "next": [
            "exit"
          ],
          "exit": false
        },
        {
          "id": "exit",
          "name": "星域出口",
          "x": 1,
          "y": 0.5,
          "eventId": "",
          "next": [],
          "exit": true
        }
      ]
    },
    {
      "id": "sector_2",
      "name": "赤红航道",
      "description": "探测舱航路 / 请选择一条分支并向右侧出口前进。",
      "startNodeId": "entry",
      "nextSectorId": "sector_3",
      "nodes": [
        {
          "id": "entry",
          "name": "跃迁入口",
          "x": 0,
          "y": 0.5,
          "eventId": "",
          "next": [
            "signal_a",
            "signal_b",
            "signal_c"
          ],
          "exit": false
        },
        {
          "id": "signal_a",
          "name": "赤红星域",
          "x": 0.25,
          "y": 0.08,
          "eventId": "show_4",
          "next": [
            "salvage",
            "trade"
          ],
          "exit": false
        },
        {
          "id": "signal_b",
          "name": "交战空域",
          "x": 0.25,
          "y": 0.5,
          "eventId": "show_5",
          "next": [
            "salvage",
            "trade"
          ],
          "exit": false
        },
        {
          "id": "signal_c",
          "name": "轨道加油站",
          "x": 0.25,
          "y": 0.92,
          "eventId": "show_6",
          "next": [
            "salvage",
            "trade"
          ],
          "exit": false
        },
        {
          "id": "salvage",
          "name": "漂流物资",
          "x": 0.5,
          "y": 0.23,
          "eventId": "salvage",
          "next": [
            "relay"
          ],
          "exit": false
        },
        {
          "id": "trade",
          "name": "补给信标",
          "x": 0.5,
          "y": 0.77,
          "eventId": "trade",
          "next": [
            "relay"
          ],
          "exit": false
        },
        {
          "id": "relay",
          "name": "数据中继",
          "x": 0.76,
          "y": 0.5,
          "eventId": "relay",
          "next": [
            "exit"
          ],
          "exit": false
        },
        {
          "id": "exit",
          "name": "星域出口",
          "x": 1,
          "y": 0.5,
          "eventId": "",
          "next": [],
          "exit": true
        }
      ]
    },
    {
      "id": "sector_3",
      "name": "回收边界",
      "description": "探测舱航路 / 请选择一条分支并向右侧出口前进。",
      "startNodeId": "entry",
      "nextSectorId": "",
      "nodes": [
        {
          "id": "entry",
          "name": "跃迁入口",
          "x": 0,
          "y": 0.5,
          "eventId": "",
          "next": [
            "signal_a",
            "signal_b",
            "signal_c"
          ],
          "exit": false
        },
        {
          "id": "signal_a",
          "name": "星海鲸群",
          "x": 0.25,
          "y": 0.08,
          "eventId": "show_7",
          "next": [
            "salvage",
            "trade"
          ],
          "exit": false
        },
        {
          "id": "signal_b",
          "name": "另一艘你",
          "x": 0.25,
          "y": 0.5,
          "eventId": "show_8",
          "next": [
            "salvage",
            "trade"
          ],
          "exit": false
        },
        {
          "id": "signal_c",
          "name": "耗材分拣站",
          "x": 0.25,
          "y": 0.92,
          "eventId": "show_9",
          "next": [
            "salvage",
            "trade"
          ],
          "exit": false
        },
        {
          "id": "salvage",
          "name": "漂流物资",
          "x": 0.5,
          "y": 0.23,
          "eventId": "salvage",
          "next": [
            "relay"
          ],
          "exit": false
        },
        {
          "id": "trade",
          "name": "补给信标",
          "x": 0.5,
          "y": 0.77,
          "eventId": "trade",
          "next": [
            "relay"
          ],
          "exit": false
        },
        {
          "id": "relay",
          "name": "数据中继",
          "x": 0.76,
          "y": 0.5,
          "eventId": "relay",
          "next": [
            "exit"
          ],
          "exit": false
        },
        {
          "id": "exit",
          "name": "星域出口",
          "x": 1,
          "y": 0.5,
          "eventId": "",
          "next": [],
          "exit": true
        }
      ]
    }
  ],
  "events": [
    {
      "id": "show_1",
      "type": "setpiece",
      "title": "星球解体",
      "description": "观测星球正在崩解。舷窗外的裂缝迅速扩张，碎片散入航路。记录这一切，或冒险投入补给收集样本。",
      "setpiece": 1,
      "duration": 6,
      "choices": [
        {
          "id": "survey",
          "text": "投入补给，深入采集",
          "result": "额外样本和传感器数据已封存。",
          "costs": [
            {
              "resourceId": "supplies",
              "amount": 2
            }
          ],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 7
            },
            {
              "resourceId": "scrap",
              "amount": 2
            }
          ]
        },
        {
          "id": "record",
          "text": "记录观测并继续",
          "result": "观测记录已收入航行日志。",
          "costs": [],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 3
            }
          ]
        }
      ]
    },
    {
      "id": "show_2",
      "type": "setpiece",
      "title": "陌生访客",
      "description": "一艘不明飞行物停在观察窗外，驾驶员正在挥手。舱内终端未找到可用的交流协议。",
      "setpiece": 2,
      "duration": 6,
      "choices": [
        {
          "id": "survey",
          "text": "投入补给，深入采集",
          "result": "额外样本和传感器数据已封存。",
          "costs": [
            {
              "resourceId": "supplies",
              "amount": 2
            }
          ],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 7
            },
            {
              "resourceId": "scrap",
              "amount": 2
            }
          ]
        },
        {
          "id": "record",
          "text": "记录观测并继续",
          "result": "观测记录已收入航行日志。",
          "costs": [],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 3
            }
          ]
        }
      ]
    },
    {
      "id": "show_3",
      "type": "setpiece",
      "title": "舱体撞击",
      "description": "撞击切断了主电路。前往 B 墙取下维修锤，再修复 D 墙的三处裂痕。全部修复后才能继续航行。",
      "setpiece": 3,
      "duration": 4,
      "choices": [
        {
          "id": "repair",
          "text": "确认维修完成，回收损坏零件",
          "result": "三处裂痕完成封补，应急电路恢复。",
          "costs": [],
          "rewards": [
            {
              "resourceId": "scrap",
              "amount": 3
            }
          ]
        }
      ]
    },
    {
      "id": "show_4",
      "type": "setpiece",
      "title": "赤红星域",
      "description": "跃迁终点是一片赤红星域。熔岩世界之间，巨大的眼睛正注视着探测舱。选择记录，或者消耗补给进行深度扫描。",
      "setpiece": 4,
      "duration": 8,
      "choices": [
        {
          "id": "survey",
          "text": "投入补给，深入采集",
          "result": "额外样本和传感器数据已封存。",
          "costs": [
            {
              "resourceId": "supplies",
              "amount": 2
            }
          ],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 7
            },
            {
              "resourceId": "scrap",
              "amount": 2
            }
          ]
        },
        {
          "id": "record",
          "text": "记录观测并继续",
          "result": "观测记录已收入航行日志。",
          "costs": [],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 3
            }
          ]
        }
      ]
    },
    {
      "id": "show_5",
      "type": "setpiece",
      "title": "交战空域",
      "description": "红蓝战机持续交火。你只是路过的生物传感器，没有收到参战指令。观察弹道，寻找有价值的残骸。",
      "setpiece": 5,
      "duration": 6,
      "choices": [
        {
          "id": "survey",
          "text": "投入补给，深入采集",
          "result": "额外样本和传感器数据已封存。",
          "costs": [
            {
              "resourceId": "supplies",
              "amount": 2
            }
          ],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 7
            },
            {
              "resourceId": "scrap",
              "amount": 2
            }
          ]
        },
        {
          "id": "record",
          "text": "记录观测并继续",
          "result": "观测记录已收入航行日志。",
          "costs": [],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 3
            }
          ]
        }
      ]
    },
    {
      "id": "show_6",
      "type": "setpiece",
      "title": "轨道加油站",
      "description": "加油站的管线仍在工作。无人系统接受回收材料作为交换，排队的船只似乎已经等待了很久。",
      "setpiece": 6,
      "duration": 8,
      "choices": [
        {
          "id": "refuel",
          "text": "用回收材料换取燃料",
          "result": "补给管线已完成输送。",
          "costs": [
            {
              "resourceId": "scrap",
              "amount": 4
            }
          ],
          "rewards": [
            {
              "resourceId": "fuel",
              "amount": 6
            }
          ]
        },
        {
          "id": "record",
          "text": "记录观测并继续",
          "result": "观测记录已收入航行日志。",
          "costs": [],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 3
            }
          ]
        }
      ]
    },
    {
      "id": "show_7",
      "type": "setpiece",
      "title": "星海鲸群",
      "description": "巨鲸穿过星海，幼鲸从舱壁游入，又穿过另一面舱壁离开。仪表未检测到实体碰撞。记录它们的迁徙轨迹。",
      "setpiece": 7,
      "duration": 12,
      "choices": [
        {
          "id": "survey",
          "text": "投入补给，深入采集",
          "result": "额外样本和传感器数据已封存。",
          "costs": [
            {
              "resourceId": "supplies",
              "amount": 2
            }
          ],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 7
            },
            {
              "resourceId": "scrap",
              "amount": 2
            }
          ]
        },
        {
          "id": "record",
          "text": "记录观测并继续",
          "result": "观测记录已收入航行日志。",
          "costs": [],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 3
            }
          ]
        }
      ]
    },
    {
      "id": "show_8",
      "type": "setpiece",
      "title": "另一艘你",
      "description": "另一艘同编号探测舱停在窗外。驾驶员延迟半秒模仿你的动作，随后自行灭灯，伸手贴向窗户。",
      "setpiece": 8,
      "duration": 16,
      "choices": [
        {
          "id": "survey",
          "text": "投入补给，深入采集",
          "result": "额外样本和传感器数据已封存。",
          "costs": [
            {
              "resourceId": "supplies",
              "amount": 2
            }
          ],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 7
            },
            {
              "resourceId": "scrap",
              "amount": 2
            }
          ]
        },
        {
          "id": "record",
          "text": "记录观测并继续",
          "result": "观测记录已收入航行日志。",
          "costs": [],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 3
            }
          ]
        }
      ]
    },
    {
      "id": "show_9",
      "type": "setpiece",
      "title": "耗材分拣站",
      "description": "机械流水线将探测舱逐一夹持、扫描和贴牌。相邻舱体被送入回收通道。等候夹钳松开，接受下一次投放。",
      "setpiece": 9,
      "duration": 25,
      "choices": [
        {
          "id": "survey",
          "text": "投入补给，深入采集",
          "result": "额外样本和传感器数据已封存。",
          "costs": [
            {
              "resourceId": "supplies",
              "amount": 2
            }
          ],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 7
            },
            {
              "resourceId": "scrap",
              "amount": 2
            }
          ]
        },
        {
          "id": "record",
          "text": "记录观测并继续",
          "result": "观测记录已收入航行日志。",
          "costs": [],
          "rewards": [
            {
              "resourceId": "data",
              "amount": 3
            }
          ]
        }
      ]
    },
    {
      "id": "salvage",
      "type": "choice",
      "title": "漂流补给箱",
      "description": "一个旧式补给箱正在低速漂流。只能带走其中一组物资，剩余部分将继续留在航道上。",
      "setpiece": 0,
      "duration": 0,
      "choices": [
        {
          "id": "fuel",
          "text": "回收燃料罐",
          "result": "已回收可用燃料。",
          "costs": [],
          "rewards": [
            {
              "resourceId": "fuel",
              "amount": 3
            }
          ]
        },
        {
          "id": "rations",
          "text": "回收口粮和材料",
          "result": "已封存口粮与零件。",
          "costs": [],
          "rewards": [
            {
              "resourceId": "supplies",
              "amount": 5
            },
            {
              "resourceId": "scrap",
              "amount": 3
            }
          ]
        }
      ]
    },
    {
      "id": "trade",
      "type": "choice",
      "title": "自动补给信标",
      "description": "信标提供一次物资交换。也可以领取紧急燃料包后离开。",
      "setpiece": 0,
      "duration": 0,
      "choices": [
        {
          "id": "purchase",
          "text": "交换燃料与补给",
          "result": "材料交换完成。",
          "costs": [
            {
              "resourceId": "scrap",
              "amount": 3
            }
          ],
          "rewards": [
            {
              "resourceId": "fuel",
              "amount": 5
            },
            {
              "resourceId": "supplies",
              "amount": 4
            }
          ]
        },
        {
          "id": "emergency",
          "text": "领取紧急燃料",
          "result": "紧急燃料已转入储存罐。",
          "costs": [],
          "rewards": [
            {
              "resourceId": "fuel",
              "amount": 2
            }
          ]
        }
      ]
    },
    {
      "id": "relay",
      "type": "choice",
      "title": "残缺的数据中继",
      "description": "旧中继愿意接收观测数据，换取下一段航路的物资配额。也可以保留数据。",
      "setpiece": 0,
      "duration": 0,
      "choices": [
        {
          "id": "upload",
          "text": "提交 5 份数据，换取补给",
          "result": "观测数据已上传，补给配额已到账。",
          "costs": [
            {
              "resourceId": "data",
              "amount": 5
            }
          ],
          "rewards": [
            {
              "resourceId": "supplies",
              "amount": 5
            },
            {
              "resourceId": "scrap",
              "amount": 4
            }
          ]
        },
        {
          "id": "keep",
          "text": "保留数据，继续航行",
          "result": "中继信号逐渐远去。",
          "costs": [],
          "rewards": []
        }
      ]
    }
  ]
};
