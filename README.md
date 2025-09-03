# Live Camera Processing System

H? th?ng x? l� 12 camera RTSP ??ng th?i v?i ki?n tr�c ?a ti?n tr�nh ?? ??m b?o hi?u n?ng v� ?n ??nh cao.

## C?u tr�c d? �n

### CameraWorker
- Ti?n tr�nh chuy�n d?ng ?? gi?i m� m?t lu?ng camera RTSP
- S? d?ng FFmpeg.AutoGen cho vi?c decode video v?i h? tr? t?ng t?c ph?n c?ng (CUDA/QSV/D3D11VA)
- Ghi d? li?u frame v�o Memory-Mapped File ?? chia s? v?i ti?n tr�nh ch�nh

### CameraManager  
- Giao di?n ch�nh qu?n l� 12 camera
- S? d?ng LittleForker ?? gi�m s�t v� kh?i ??ng l?i c�c ti?n tr�nh CameraWorker
- Hi?n th? video t? 12 camera trong layout 4x3
- T? ??ng kh?i ??ng l?i worker khi g?p s? c?

## Y�u c?u h? th?ng

- .NET 8.0
- Windows 10/11 (x64)
- FFmpeg libraries (?� ???c bao g?m trong d? �n)
- GPU h? tr? CUDA, Intel QSV ho?c DirectX (t�y ch?n, cho t?ng t?c ph?n c?ng)

## C�ch s? d?ng

1. **C?u h�nh URL camera**: M? `CameraManager\Form1.cs` v� thay th? c�c URL RTSP trong m?ng `_rtspUrls` b?ng ??a ch? th?c t? c?a camera.

```csharp
private readonly List<string> _rtspUrls = new List<string>
{
    "rtsp://192.168.1.100:554/stream1",
    "rtsp://192.168.1.101:554/stream1",
    // ... th�m 10 URL kh�c
};
```

2. **Build d? �n**: Ch?n c?u h�nh **x64** (quan tr?ng cho FFmpeg libraries) v� build solution.

3. **Ch?y ?ng d?ng**: Kh?i ch?y CameraManager.exe

## T�nh n?ng ch�nh

- **Ki?n tr�c ?a ti?n tr�nh**: M?i camera ch?y trong ti?n tr�nh ri�ng, tr�nh ?nh h??ng l?n nhau
- **T?ng t?c ph?n c?ng**: H? tr? CUDA, Intel QSV, DirectX cho decode video
- **T? ph?c h?i**: T? ??ng kh?i ??ng l?i worker khi g?p l?i
- **Hi?u n?ng cao**: S? d?ng Memory-Mapped File ?? chia s? d? li?u nhanh ch�ng
- **?? tr? th?p**: T?i ?u h�a cho ?ng d?ng real-time

## Ghi ch� k? thu?t

- Frame size m?c ??nh: 1920x1080 BGR24
- Frame rate hi?n th?: ~30 FPS
- Memory-mapped file size: ~6MB per camera
- Hardware fallback: T? ??ng chuy?n sang software decoder n?u hardware kh�ng kh? d?ng

## Troubleshooting

1. **L?i FFmpeg DLL**: ??m b?o build d? �n ? mode x64
2. **Camera kh�ng k?t n?i**: Ki?m tra URL RTSP v� k?t n?i m?ng
3. **Hi?u n?ng th?p**: Ki?m tra driver GPU v� b?t hardware acceleration
4. **Memory usage cao**: ?i?u ch?nh s? l??ng camera ho?c resolution