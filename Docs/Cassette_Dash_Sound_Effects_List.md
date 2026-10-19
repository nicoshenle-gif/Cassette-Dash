# 《Cassette Dash!》游戏音效设计需求清单 (Sound Effects Checklist)

> **游戏类型**：2.5D 跑酷平台跳跃 / 微观机械物理  
> **核心主题**：复古卡式磁带内部的微缩冒险 (Inside a Vintage Cassette Tape)  
> **声音美学定位**：复古电气、Lo-fi / Analogue Tape 质感、微型精密机械齿轮、亚克力塑料空腔共鸣  

---

## 优先级定义 (Priority Levels)
- **P0（核心必备）**：直接影响操作手感、核心玩法反馈与伤害警示，第一阶段必须具备。
- **P1（沉浸提升）**：丰富机械细节与空间物理表现，增强视听一致性。
- **P2（细节润色）**：环境微调、细微动作修饰与 UI 质感提升。

---

## 一、 主角动作与角色状态 (Player & Movement)

| 音效标识 / 英文名 | 触发条件 (关联脚本事件) | 声音设计与质感描述 | 优先级 |
| :--- | :--- | :--- | :---: |
| `Footstep_Tape` | 在磁带表面奔跑/走动 | 轻盈柔和的鞋底踩在聚酯磁带薄膜上的微哑摩擦声。建议 3~4 个变体，随机音高（Pitch 0.95~1.05）。 | **P0** |
| `Footstep_Plastic` | 在磁带盒底壳塑料面上移动 | 清脆、带有轻微空腔硬质反弹质感的微型脚步声。 | **P0** |
| `Footstep_Crouch` | 按住 `Ctrl` 俯身下蹲移动 | 极轻微的布料与微弱滑行摩擦声（音量约为普通行走的 40%）。 | **P2** |
| `Player_Jump` | 处于地面按 `Space` 起跳瞬间 | 弹性质感的干脆蹬地破风声，带有一丝复古微型弹簧跳跃感。 | **P0** |
| `Player_Land_Soft` | 普通起跳下落着地 (`fallDist < 1.25m`) | 软着陆微弱缓冲声，无硬冲击。 | **P1** |
| `Player_Land_Hard_Fall` | 坠落高度超过阈值 (`fallDist >= 1.25m / 5.6m`) | **沉重的骨骼/肉体撞击地面的闷响**，伴随角色重摔喘息声，传达严重的失重着陆惩罚感。 | **P0** |
| `Player_Hurt` | 碰撞障碍扣除 50 HP (`TakeDamage`) | 钝击重击声 + 身体短促吃痛闷哼 + 微弱瞬态电流/低音震荡（配合屏幕红闪与击退）。 | **P0** |
| `Player_Die_Knockdown` | 血量归零触发击倒死亡动画 | 沉重倒地扑倒声，叠加强烈的**“磁带走调慢速停转（Tape Slowdown Pitch Drop）”**标志性音效。 | **P0** |
| `Player_Respawn` | 按 `R` 或倒计时重置复活 (`Respawn`) | **复古“磁带高速倒带 (Fast Rewind Whirr)”+ 录音机重型机械按键弹起复位（Click-Clack）**，高度点题。 | **P0** |

---

## 二、 磁带与卷盘物理交互 (Cassette Tape & Mechanics)

| 音效标识 / 英文名 | 触发条件 (关联脚本事件) | 声音设计与质感描述 | 优先级 |
| :--- | :--- | :--- | :---: |
| `Tape_Feed_Slack_Loop` | **按住 E 键加速左卷盘出带时** (`boostLeftReel = true`) | **核心玩法音效**：左轮盘急速旋转的高频齿轮转动声、马达提速蜂鸣与磁带飞速剥离的“哧哧”声（松开 E 键平滑淡出）。 | **P0** |
| `Tape_Slack_Takeup` | 松开 E 键右轮卷收多余磁带 | 逐渐收紧的尼龙/薄膜绷紧微鸣声（类似琴弦旋紧时的音高上升感）。 | **P1** |
| `Tape_Hammock_Bounce` | 角色站上/跳入松弛吊床导致 Verlet 节点大幅下沉 | 强韧绳索受力拉伸、下沉弹回的深沉气流/晃动摆动声，强化“蹦床/吊床”受力反馈。 | **P1** |
| `Reel_Spinning_Loop` | 磁带左右卷盘常态转动 (`3D 空间音效`) | 持续平稳的微型塑料齿轮咬合与轴承旋转声，左右声道可随卷盘位置空间定位。 | **P1** |

---

## 三、 机关与跑酷障碍物 (Obstacles & Hazards)

| 音效标识 / 英文名 | 触发条件 (关联脚本事件) | 声音设计与质感描述 | 优先级 |
| :--- | :--- | :--- | :---: |
| `Obstacle_Head_Slam` | 磁头活塞下砸到底部磁带 (`CassetteHeadObstacle`) | **极有份量感的金属快速下冲 + 羊毛毡垫猛击磁带的沉闷重响**（Punchy Mechanical Clunk + Felt Thud）。 | **P0** |
| `Obstacle_Head_Rise` | 磁头向上回升安全高度 (`moveUpDuration = 0.75s`) | 两侧 V 型弹簧拉伸复位的金属微鸣声与滑轨抽吸声。 | **P1** |
| `PinSpike_Warning` | 导柱尖刺弹出前预警 (`warningDuration = 0.35s`) | 机械锁死前的快速齿轮卡扣咔哒声，给玩家留出 0.35 秒反应起跳/闪避时间。 | **P0** |
| `PinSpike_Thrust` | 尖刺在 0.14s 极速弹出 (`thrustUpDuration`) | **极为清脆凌厉、穿透力极强的金属刺刀弹射出鞘声**（Sharp Metallic Blade Snap）。 | **P0** |
| `PinSpike_Retract` | 尖刺平稳收缩回圆柱凹槽 | 顺滑的机械金属插销回缩滑动声。 | **P2** |
| `Dropper_Arm_Extend` / `Retract` | 顶部螺线管机械臂向下伸展/向上收回 | 小型伺服电机升降嗡鸣与气动推杆细微滑行声（Pneumatic Servo Whine）。 | **P1** |
| `Dropper_Aim_Warning` | 投放前黄色警戒光圈充能 (`aimChargeDuration = 0.32s`) | 电子蜂鸣警报音（急促的 2~3 声脉冲高频哔哔声）。 | **P0** |
| `Dropper_Release` | 释放机械零件危险品瞬间 | 微型电磁铁断电脱扣声（Crisp Magnetic Release Clack）。 | **P1** |
| `Hazard_Floor_Impact` | 散落的十字螺丝/黄铜齿轮/铜线圈砸到底面 | 精细清脆的小金属零件在塑料硬壳上多次轻跳弹跃的“叮铃”落地声。 | **P1** |

---

## 四、 环境氛围与背景音乐 (Ambience & BGM)

| 音效标识 / 英文名 | 触发条件 | 声音设计与质感描述 | 优先级 |
| :--- | :--- | :--- | :---: |
| `Ambience_TapeHiss_Loop` | 全程低音量环境循环 (可置于 AudioMixer 底层) | **经典复古磁带空带白噪底噪（Analogue Tape Hiss）**，烘托浓郁复古模拟录音质感。 | **P1** |
| `Ambience_Shell_Resonance` | 空间环境滤镜/混响 | 微型风流吹过磁带盒开孔引起的轻微空灵空腔共振音。 | **P2** |
| `Music_Level_Synthwave` | 关卡游玩时循环播放 | 风格建议：**Synthwave / Retrowave / Chiptune Lo-Fi**，BPM 推荐 110~125，兼具律动与跑酷张力。 | **P1** |

---

## 五、 UI 与系统交互反馈 (UI & System Feedback)

| 音效标识 / 英文名 | 触发条件 | 声音设计与质感描述 | 优先级 |
| :--- | :--- | :--- | :---: |
| `UI_LowHealth_Beep` | HP 低于 25 且血条剧烈闪烁时 | 沉闷的心跳脉冲声或电子监护仪低血报警声（Low Health Pulsing）。 | **P1** |
| `UI_Cassette_Button` | 菜单点击、确认、重新开始 | 录音机实木/金属琴键式按键重重按下的物理按压“喀哒”声（Tactile Button Click）。 | **P2** |

---

## 六、 音频实现建议与工程规范

1. **音频格式与采样率**：
   - 短音效 (SFX)：16-bit / 24-bit 44.1kHz 或 48kHz 无损 `.wav`
   - 长循环音效 (Ambience / BGM)：压缩高质量 `.ogg` 或 `.mp3`
2. **随机化处理 (Anti-Fatigue)**：
   - 针对高频出现的脚步声 (`Footstep`)、受击声 (`Hurt`)、零件砸地声 (`Hazard_Impact`)，务必制作 3~5 种不同 Variations，播放时赋予 `±5% ~ ±10%` 的随机音高 (Pitch) 与音量抖动。
3. **3D 空间衰减 (Spatial Blend)**：
   - `CassetteHeadObstacle`（磁头下砸）、`CassettePinSpikeObstacle`（导柱升降刺）、`CassetteOverheadDropper`（上方投放器）建议开启 3D Spatial Blend（0.7 ~ 1.0），Max Distance 设为 12~15 个 Unity 单位，实现清晰的位置辨识感。
4. **推荐音频总线分类 (Audio Mixer Groups)**：
   - `Master`
     - `BGM` (背景音乐)
     - `SFX`
       - `Player` (主角动作/受击)
       - `Mechanics` (磁带物理/出带)
       - `Hazards` (障碍机关)
     - `Ambience` (磁带底噪/空腔环境)
     - `UI` (菜单与血量警报)
