# CardReforgeQueueMod

# English

A card upgrade queue mod for `Slay the Spire 2`.

You can place unupgraded cards into a queue and automatically upgrade them in queue order from the smith button at rest sites.

## Features

- Adds an upgrade queue button to the left of the map button on the top bar.
- Shows only upgradable, unupgraded cards in the upgrade queue UI.
- Supports drag and drop to add cards to or remove cards from the queue.
- Supports drag and drop reordering inside the queue.
- Groups identical cards into a single row and displays the count as `xN`.
- Cards with different enchantments are displayed as separate groups.
- Displays a queue preview UI under the smith button at rest sites.
- Supports drag and drop reordering from the rest site queue preview UI.
- The rest site queue preview contains the full queue, but only about 3 items are visible at once and the list can be scrolled.
- If `Auto upgrade` is checked, selecting smith immediately upgrades the top card in the queue.
- If `Auto upgrade` is unchecked, the game's default smith card selection screen is used.
- Once a card is upgraded, it is immediately removed from both the queue list and the card list.
- Queue saves are separated between single-player and multiplayer runs.

## Usage

1. Start the game and press the upgrade queue button on the top bar from a combat/room screen.
2. Drag a card from the `Cards` list on the right into the `Queue` list on the left.
3. Drag cards inside the queue to adjust upgrade priority.
4. Go to a rest site.
5. Check the `Auto upgrade` state under the smith button.
6. Press the smith button.

If `Auto upgrade` is enabled, the top card in the queue is upgraded immediately.

## UI Behavior

### Upgrade Queue Popup

- Open and close it with the button to the left of the top bar map button.
- The left side is `Queue`; the right side is `Cards`.
- `Queue` keeps the saved order.
- `Cards` is sorted by card name.
- Pressing the settings button closes the upgrade queue popup.

### Rest Site UI Under The Smith Button

- Displayed under the smith button at rest sites.
- Shows the auto upgrade checkbox and queue preview.
- The queue preview contains the full queue, but the visible area is limited to about 3 rows.
- The list can be scrolled.

# Korean

`Slay the Spire 2`용 카드 강화 큐 모드입니다.

강화되지 않은 카드를 큐에 넣어 두고, 휴식 장소의 제련 버튼에서 큐 순서대로 자동 강화할 수 있습니다.

## 주요 기능

- 상단바의 맵 버튼 왼쪽에 업그레이드 큐 버튼을 추가합니다.
- 업그레이드 큐 UI에서 강화 가능한 미강화 카드만 표시합니다.
- 카드를 드래그 앤 드롭해서 큐에 넣거나 제거할 수 있습니다.
- 큐 안의 카드 순서를 드래그 앤 드롭으로 변경할 수 있습니다.
- 같은 카드는 하나의 행으로 묶어서 `xN` 형식으로 표시합니다.
- 인첸트가 다른 카드는 서로 다른 묶음으로 표시합니다.

- 휴식 장소의 제련 버튼 밑에 큐 미리보기 UI를 표시합니다.
- 제련 버튼 밑 UI에서도 큐 순서를 드래그 앤 드롭으로 변경할 수 있습니다.
- 제련 버튼 밑 UI는 큐 전체를 가지고 있지만, 한 번에 최대 3개 정도만 보이고 스크롤할 수 있습니다.
- `Auto upgrade`가 체크되어 있으면 제련 선택 시 큐의 가장 위 카드를 바로 강화합니다.
- `Auto upgrade`가 체크되어 있지 않으면 게임 기본 제련 선택 화면을 사용합니다.

- 카드가 강화되면 큐 목록과 카드 목록에서 바로 제거됩니다.

- 싱글게임과 멀티게임의 큐 저장 위치를 분리합니다.

## 사용 방법

1. 게임 실행 후 전투/방 화면 상단바에서 업그레이드 큐 버튼을 누릅니다.
2. 오른쪽 `Cards` 목록에서 원하는 카드를 왼쪽 `Queue` 목록으로 드래그합니다.
3. 큐 안에서 카드를 드래그해 강화 우선순위를 조정합니다.
4. 휴식 장소로 이동합니다.
5. 제련 버튼 밑의 `Auto upgrade` 체크 상태를 확인합니다.
6. 제련 버튼을 누릅니다.

`Auto upgrade`가 켜져 있으면 큐 맨 위 카드가 즉시 강화됩니다.

## UI 동작

### 업그레이드 큐 팝업

- 상단바 맵 버튼 왼쪽의 버튼으로 열고 닫습니다.
- 왼쪽은 `Queue`, 오른쪽은 `Cards`입니다.
- `Queue`는 저장된 순서를 유지합니다.
- `Cards`는 카드 이름순으로 표시합니다.
- 설정 버튼을 누르면 업그레이드 큐 팝업은 닫힙니다.

### 제련 버튼 밑 UI

- 휴식 장소에서 제련 버튼 밑에 표시됩니다.
- 자동 강화 체크박스와 큐 미리보기를 표시합니다.
- 큐 미리보기에는 큐 전체가 들어가며, 보이는 영역은 약 3개 행 높이로 제한됩니다.
- 목록은 스크롤할 수 있습니다.
