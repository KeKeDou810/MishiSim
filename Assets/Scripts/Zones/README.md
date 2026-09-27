# 战斗区域视图

`CardPlacementZoneView` 统一处理鼠标进入/离开、点击、拖入请求和卡牌摆放。子类为 `BoardNodeView`、`DeckZoneView`、`DiscardZoneView`、`ContractZoneView`、`OffFieldZoneView`。

## 场景配置

CardTestGym 已挂接 21 个区域（13 个圆阵），并添加 CardAnchor 和摄像机 PhysicsRaycaster。两个 EmptyZone 暂作为场外区。Host OwnerId 为 0，Client 为 1，无归属为 -1。

- ZoneId 在同一场景内必须唯一；NodeId 在圆阵之间必须唯一。
- BoardNodeView.Neighbours 配置实际相连的圆阵。构建规则地图时连接是双向的，只需配置一次。当前列表留空，须按地图连线填写。
- OffFieldZoneView.AffectedNodes 配置场外策略卡可以影响的圆阵，与攻击邻接独立；当前留空。
- CardAnchor 定义摆放原点及朝向。卡牌正面沿局部 -Z，叠放也沿 -Z；位移不随区域缩放变化。
- Layout 支持 Stack、Row、Slots。VisualCapacity 是视图容量，0 表示不限，不代替规则校验。超频需要显示叠卡时，应相应增加圆阵容量。
- HoverHighlight 可选，指定额外的高亮子物体；不要指定区域物体自身。

## 控制器接入

1. 在区域启用后（例如控制器 Start）使用 `ZoneRegistry.InScene(gameObject.scene)` 获取区域，订阅 `Clicked`、`HoverChanged`、`PlacementRequested`。
2. 卡牌实例先调用 `Initialize(hostAssignedId)`，再 `AssignDefinition(definition)`。所有端使用主机分配的同一实例 ID。
3. 主机/规则确认移动后调用目标区域的 `PlaceApproved(card)`，检查返回值。成功后会自动移出原区域并重排两边。该调用只更新视图，不更新或同步 BattleState。
4. Card prefab 已带 BattleCardPointer。控制器根据本地玩家权限设置 `CanPreview` 和 `CanDrag`；默认均关闭，避免泄露背面卡牌。
5. 拖入只触发 PlacementRequested；未获批准时回原位。规则控制器需验证行动方、回合阶段、费用、目标和容量，再同步结果、更新视图。
6. 配好 Neighbours 后调用 `ZoneRegistry.BuildBattleBoard(scene)` 建立规则地图。场外范围从 `AffectedNodes` 读取，供 Lua 效果层查询；此组件不会自动执行效果。

当前实现是区域交互与摆放基础，不包含完整的战斗控制器或网络移动请求。
