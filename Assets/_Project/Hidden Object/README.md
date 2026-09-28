# Tìm điểm khác biệt — level bằng SpriteRenderer

## Thư mục

```text
Assets/_Project/Hidden Object/Prefabs/Level/
  LevelBase/LevelBase.prefab        Mẫu trống để tạo màn
  Level 1.prefab                   Màn đang chơi
  Level 2.prefab ...               Các màn mới

Assets/_Project/Hidden Object/
  Script/Runtime/                  Gameplay và hiển thị world
  Script/Editor/                   Level Editor và kiểm tra
  Prefabs/Elements/DifferenceSpot.prefab
  Sprite/                         Ảnh và vòng đánh dấu
  Scenes/DifferencePreview.unity   Scene thử độc lập
```

Level Loop Sort cũ nằm trong `Assets/_Project/Legacy/LoopSort/Levels`, đã bỏ khỏi Addressables đang chơi. Công cụ cũ ở menu **Tools > Legacy** và chỉ ghi vào thư mục lưu trữ này.

## Prefab và script

```text
GameplayScene
  WorldCamera                     Camera orthographic + Physics2DRaycaster
  LevelRoot                       Transform thường, nơi tải màn
    Level 1                       Level + DifferenceLevelController + DifferenceWorldBoard
      Board
        Picture A                 DifferencePanelView
          Artwork                 SpriteRenderer + BoxCollider2D nhận click
            Spot_01               DifferenceSpotView + BoxCollider2D vùng đáp án
              FoundMarker         SpriteRenderer vòng đánh dấu
            Spot_02 ...
        Picture B                 Cấu trúc tương tự A
```

Level không có Canvas, Image hay RectTransform. Picture A/B là object trong prefab màn, không cần prefab riêng. HUD dùng UI cho tiến độ, lượt sai, hint và pause.

| Script | Gắn vào | Tham chiếu / nhiệm vụ |
| --- | --- | --- |
| Level | Root màn | Difference Controller và World Board; nối luồng game chính |
| DifferenceLevelController | Root màn | Panel A/B, danh sách cặp, Max Mistakes; đúng/sai/hint/thắng/thua |
| DifferenceWorldBoard | Root màn | Content Root = Board; fit bảng vào vùng camera dành cho gameplay |
| DifferencePanelView | Picture A/B | Picture = SpriteRenderer Artwork; Picture Collider = collider Artwork |
| DifferenceSpotView | Từng Spot | Picture, Hit Area, Found Marker, Normalized Bounds |
| DifferencePair | Không gắn component | Class serialize: Id, Spot A, Spot B |
| DifferencePreview | HUD scene thử | Board, World Board, World Camera, text và nút thử |

`LevelController` trong GameplayScene có **Level Root** và **World Camera** kéo sẵn. Nó tải prefab bằng Addressables dưới LevelRoot rồi truyền camera vào màn. Camera không nằm trong prefab level.

Ảnh, vùng click và marker đều có sẵn trong prefab. Gameplay mới không dùng singleton, tự tìm camera hay tạo từng object lúc chạy; LevelController tạo/release instance của cả màn. Các manager có sẵn giữ cấu trúc của project.

## Level Editor

Mở **Tools > Level Editor > Find Differences Editor** hoặc **Tools > Find Differences > Level Editor**.

1. Chọn `Assets/_Project/Hidden Object/Prefabs/Level/Level 1.prefab`, bấm **Open** để mở Prefab Mode.
2. Kéo sprite vào **Picture A / Picture B**, hoặc thả lên ảnh xem trước.
3. Bật **Draw New Pair**, kéo chuột khoanh vùng đáp án trên ảnh. Công cụ tạo và ghép hai Spot.
4. Tắt chế độ vẽ. Bấm vùng có số để chọn; kéo bên trong để di chuyển, kéo góc dưới phải để đổi kích thước. **Sync A/B areas while editing** bật mặc định: kéo vùng ở ảnh trên hay dưới thì vùng còn lại nhận cùng tọa độ tương đối. Tắt tùy chọn này để chỉnh A/B độc lập khi chi tiết bị lệch.
5. Có thể đổi Id, chọn Spot trong Hierarchy, xóa cả cặp, copy vùng A sang B hoặc ngược lại. Trong Scene view, Inspector của Spot cũng có tùy chọn Sync tương tự. **Ctrl+Z / Ctrl+Y** để Undo/Redo.
6. Bấm **Validate**, rồi **Save Prefab + Sync Levels** để lưu và cập nhật Addressables/LevelConfig.

Tạo màn mới: nhập số rồi bấm **Create Empty Level**. Công cụ dùng `Prefabs/Level/LevelBase/LevelBase.prefab`, không ghi đè màn có sẵn. Đặt tên liên tục `Level 1`, `Level 2`, ... Màn nháp thiếu ảnh/đáp án chưa được cộng vào tiến trình chơi.

Prefab Mode vẫn dùng thiết lập Auto Save của Unity. Tắt Auto Save nếu muốn chỉ lưu khi chủ động bấm Save.

## Kéo ảnh và chỉnh vị trí trực tiếp

- Import ảnh với **Texture Type = Sprite (2D and UI)**, **Sprite Mode = Single**, rồi Apply.
- Mở `Board/Picture A/Artwork`, kéo ảnh vào **SpriteRenderer > Sprite**. Làm tương tự cho B.
- **Frame Size** trên DifferencePanelView là kích thước khung theo world unit; ảnh tự fit và giữ tỉ lệ.
- Di chuyển `Picture A` / `Picture B` bằng Transform để đổi bố cục. Nếu đổi khung/bố cục, chỉnh **Reference Size** trên DifferenceWorldBoard để bao đủ bảng.
- Chọn Spot trong Hierarchy và bật Gizmos. Viền cam là vùng click; dùng handle xanh trong Scene view để kéo tâm/cạnh, hoặc sửa **Normalized Bounds** trong Inspector.

`Normalized Bounds` tính theo tỉ lệ sprite: góc trái dưới (0,0), góc phải trên (1,1). Dữ liệu lưu ngay trong prefab; không cần JSON hay database riêng. Vùng click giữ đúng vị trí tương đối khi đổi PPU, fit ảnh hoặc đổi tỉ lệ màn hình.

BoxCollider2D trên Spot chứa hình học vùng hit và được tắt để không tranh raycast với Artwork. Chỉ collider Artwork nhận raycast vật lý. Controller kiểm tra vùng đáp án rồi đánh dấu cả hai bên. Không chỉnh trực tiếp collider/marker vì kích thước của chúng được tính từ Normalized Bounds.

Mỗi cặp cần Id duy nhất, Spot A/B thuộc đúng ảnh. Nếu vật bị thiếu ở B, đặt Spot B lên chỗ trống tương ứng. Tránh vùng chồng nhau: vùng đã tìm được ưu tiên bỏ qua click; vùng chưa tìm được xét theo thứ tự danh sách.

DifferenceWorldBoard chỉ đổi Transform của Board, không tạo object. Tắt **Fit To Camera** nếu muốn giữ tọa độ/kích thước world đã đặt. Với chế độ fit mặc định, **Viewport Area** chừa khoảng cho HUD.

## Chơi thử

- Mở `Hidden Object/Scenes/DifferencePreview.unity` rồi Play để thử độc lập.
- Game chính dùng LoadingScene → GameplayScene.
- Tìm đúng ở một bên đánh dấu cả A/B. Chạm lại không cộng điểm hoặc trừ lượt.
- **Max Mistakes = 0**: không giới hạn sai. Hint hiện vòng vàng, không tự cộng tiến độ.
- Pause chặn click. Ẩn PopupInGame sẽ ẩn bảng world; replay reset tiến độ và marker.
- Chưa dùng timer. Khi build game, build lại Addressables theo quy trình project.

## Kiểm tra trong Unity

- **Tools > Find Differences > Verify Sample Gameplay**: kiểm tra SpriteRenderer, click Physics2D, đúng/sai/hint, pause/replay, thắng/thua, ẩn bảng và tham chiếu scene.
- **Verify HUD and Capture Preview**: kiểm tra bố cục và HUD không chặn ảnh; xuất `Library/DifferenceWorld-*.png` ở ba tỉ lệ màn hình.
- **Verify Level Editor**: thêm/xóa cặp, Undo/Redo, thay ảnh, chỉnh A/B độc lập, bảo vệ prefab đã có, lưu và mở lại.
