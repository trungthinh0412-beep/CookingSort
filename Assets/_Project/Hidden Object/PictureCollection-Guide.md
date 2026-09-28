# Picture Collection — prefab và data

Luồng: **PopupHome / Btn_Task → PictureCollectionPopup → PictureAlbumPopup → PicturePreviewPopup → chơi lại level**. Nút Back trượt ngược; trang thư viện và album giữ vị trí cuộn. Không tạo BottomBar_Group.

## Prefab

| Prefab | Script | Công việc |
| --- | --- | --- |
| `Assets/_Project/Prefabs/Popup/PopupHome.prefab` | `PopupHome` | Home, nút chơi chính và Btn_Task mở thư viện |
| `Prefabs/UI/Elements/HomeAlbumPicture.prefab` | `HomeAlbumPictureView` | Cover, tên album, tiến độ; layer Puzzle dùng `AlbumPuzzleGraphic` che các mảnh chưa mở |
| `Prefabs/UI/Popup/PictureCollectionPopup.prefab` | `PictureCollectionPopup` | Thư viện album, lưới 3 cột |
| `Prefabs/UI/Elements/PictureAlbumCard.prefab` | `PictureAlbumCard` | Cover, tên, số ảnh đã mở/tổng; album khóa có dấu ? |
| `Prefabs/UI/Popup/PictureAlbumPopup.prefab` | `PictureAlbumPopup` | Ảnh trong một album, lưới 2 cột và cuộn dọc |
| `Prefabs/UI/Elements/PictureLevelCard.prefab` | `PictureLevelCard` | Thumbnail; ảnh chưa hoàn thành là ô xanh và không bấm được |
| `Prefabs/UI/Popup/PicturePreviewPopup.prefab` | `PicturePreviewPopup` | Hai ảnh A/B theo chiều dọc và nút Replay |

Các đường dẫn `Prefabs/…`, `Script/…`, `Data/…` trong bảng tính từ **Assets/_Project/Hidden Object**. Ba popup được đăng ký sẵn trong `Assets/_Project/Config/PopupConfig.asset`. Các reference của Button, Image, TMP, ScrollRect và prefab card đều được serialize trong prefab. Danh sách chỉ Instantiate card từ prefab đã gán, tái dùng card khi mở lại. `ResponsivePictureGrid` tính chiều rộng ô theo viewport.

## Tạo album và thêm level

1. Tạo/đổi level trong **Tools → Find Differences → Level Editor** như trước: kéo sprite A/B và chỉnh vùng click. Gameplay vẫn dùng SpriteRenderer. Lưu prefab bằng **Save Prefab + Sync Levels**.
2. Level nằm ở **Assets/_Project/Hidden Object/Prefabs/Level/Level N.prefab**. Chỉ các level hợp lệ, liên tiếp từ Level 1 được đưa vào tiến độ chính và Addressables. LevelBase là template, không phải màn chơi.
3. Create → **Differences → Picture Album** để tạo `PictureAlbumData` trong `Data/Albums`. Đặt **Album Id** cố định, **Title**, kéo ảnh vào **Cover Sprite**. Cover riêng có thể là ảnh dọc giống mẫu bạn gửi; A/B là ảnh gameplay.
4. Trong **Levels**, thêm entry và kéo prefab level vào **Level Prefab**. Đặt **Level Number** tương ứng số level chính; không trùng và phải liên tiếp giữa các album. **Thumbnail** có thể để trống để dùng ảnh A.
5. Bấm **Sync A/B From Level Prefabs**. **Level Id** trống được điền bằng GUID của prefab; ảnh A/B được lấy trực tiếp từ SpriteRenderer trong level. Lần sau Save trong Level Editor sẽ tự cập nhật ảnh A/B cho các album đã gán level đó.
6. Mở **Data/PictureCollectionConfig.asset**, kéo album vào **Albums** theo thứ tự mở khóa. Config này đã gán cho thư viện, Home và LevelController trong GameplayScene.
7. Chạy **Tools → Find Differences → Verify Picture Collection** để kiểm tra ID, số level, prefab, ảnh A/B và reference UI.

Không đổi Album Id/Level Id sau khi phát hành. Đổi tên/di chuyển prefab không làm đổi GUID nếu giữ file .meta. Để thêm level mới, nên thêm vào album cuối trước khi tạo album tiếp theo.

## Tiến độ và chơi lại

- Tổng ảnh/mảnh tranh = số entry trong album; không cố định 25. Mỗi entry tương ứng một mảnh cover Home. Thắng lần đầu mở đúng ảnh và mảnh đó.
- Home hiển thị album chứa level chính tiếp theo. Album sau mở khi tất cả ảnh của album trước hoàn thành. Khi hoàn thành toàn bộ nội dung hiện có, Home giữ album cuối.
- Level chính tiếp tục sử dụng quy tắc loop của LevelConfig. Chơi lại một prefab đã hoàn thành trong loop không cấp thêm mảnh hoặc phần thưởng đầu tiên.
- Replay từ preview tải đúng AssetReference của ảnh đó, dùng `PicturePlaySession` riêng. Không yêu cầu/trừ tim, tăng level chính, cộng phần thưởng hay lượt quảng cáo. Thắng/thua quay lại preview, có thể Replay tiếp. Exit trong pause cũng quay lại preview.
- Các ID hoàn thành và phiên bản migration nằm trong `PlayerData.PictureCollection.cs`, lưu cùng file player_data.json mã hóa hiện có. Số ảnh hoàn thành và trạng thái khóa tính từ các ID, không lưu thêm bản sao.
- Save cũ được migrate một lần: những entry có Level Number nhỏ hơn CurrentLevelIndex được coi là đã hoàn thành. Migration không cấp tiền/thưởng.

## Mẫu hiện tại

`Data/Albums/SampleAlbum.asset` chỉ chứa **Level 1 thực tế** của dự án; chưa có 25 level hoặc bộ tranh như ảnh tham khảo. Kéo bộ ảnh/level thật của bạn vào album để có lưới đầy và cover nhiều mảnh. Các popup dùng UI Image để hiển thị ảnh; prefab gameplay vẫn là SpriteRenderer.

Menu **Setup Picture Collection UI** dùng để dựng lại prefab UI ban đầu: nó sẽ ghi lại bố cục các prefab collection và phần HomeAlbumPicture. Không chạy menu này sau khi đã custom giao diện, trừ khi muốn dựng lại. Để cập nhật ảnh khi chỉnh level, chỉ dùng **Sync Album Pictures From Levels** hoặc Save trong Level Editor.
