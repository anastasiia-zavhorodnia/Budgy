using Microsoft.Extensions.Logging;

namespace Budgy.Services;

public record PhotoResult(bool Success, string? FilePath = null, string? Error = null, bool Cancelled = false)
{
    public static PhotoResult Ok(string path) => new(true, path);
    public static PhotoResult Fail(string error) => new(false, Error: error);
    /// <summary>Користувач сам закрив камеру/галерею — це не помилка.</summary>
    public static PhotoResult Cancel() => new(false, Cancelled: true);
}

public record LocationResult(bool Success, double Latitude = 0, double Longitude = 0, string? Error = null)
{
    public static LocationResult Ok(double lat, double lon) => new(true, lat, lon);
    public static LocationResult Fail(string error) => new(false, Error: error);
}

/// <summary>Фото чека (камера / галерея) та координати місця покупки (геолокація).</summary>
public interface IReceiptService
{
    Task<PhotoResult> CapturePhotoAsync();
    Task<PhotoResult> PickPhotoAsync();
    Task<LocationResult> GetCurrentLocationAsync();
    /// <summary>Видаляє файл чека (лише з каталогу чеків додатка).</summary>
    void DeletePhoto(string? path);
}

public class ReceiptService(ILogger<ReceiptService> logger) : IReceiptService
{
    private static string ReceiptsDirectory => Path.Combine(FileSystem.AppDataDirectory, "receipts");

    // ---------------- Фото ----------------

    public async Task<PhotoResult> CapturePhotoAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                logger.LogWarning("Камера недоступна на цьому пристрої");
                return PhotoResult.Fail("На цьому пристрої камера недоступна. Оберіть фото з галереї.");
            }

            var status = await EnsurePermissionAsync<Permissions.Camera>("Камера");
            if (status != PermissionStatus.Granted)
                return PhotoResult.Fail("Немає дозволу на використання камери. Дозвольте доступ у налаштуваннях пристрою.");

            var photo = await MainThread.InvokeOnMainThreadAsync(() => MediaPicker.Default.CapturePhotoAsync());
            return await SaveAsync(photo);
        }
        catch (FeatureNotSupportedException ex)
        {
            logger.LogWarning(ex, "Камера не підтримується");
            return PhotoResult.Fail("Камера не підтримується на цьому пристрої.");
        }
        catch (PermissionException ex)
        {
            logger.LogWarning(ex, "Відмовлено в дозволі на камеру");
            return PhotoResult.Fail("Немає дозволу на використання камери.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося зробити фото чека");
            return PhotoResult.Fail("Не вдалося зробити фото. Спробуйте ще раз.");
        }
    }

    public async Task<PhotoResult> PickPhotoAsync()
    {
        try
        {
            var photo = await MainThread.InvokeOnMainThreadAsync(() => MediaPicker.Default.PickPhotoAsync());
            return await SaveAsync(photo);
        }
        catch (PermissionException ex)
        {
            logger.LogWarning(ex, "Відмовлено в доступі до галереї");
            return PhotoResult.Fail("Немає дозволу на доступ до фото.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося обрати фото з галереї");
            return PhotoResult.Fail("Не вдалося обрати фото. Спробуйте ще раз.");
        }
    }

    private async Task<PhotoResult> SaveAsync(FileResult? photo)
    {
        if (photo is null) return PhotoResult.Cancel();

        var extension = Path.GetExtension(photo.FileName);
        if (string.IsNullOrEmpty(extension)) extension = ".jpg";

        Directory.CreateDirectory(ReceiptsDirectory);
        var path = Path.Combine(ReceiptsDirectory, $"receipt_{DateTime.Now:yyyyMMdd_HHmmss_fff}{extension}");

        await using var source = await photo.OpenReadAsync();
        await using var target = File.Create(path);
        await source.CopyToAsync(target);

        logger.LogInformation("Збережено фото чека: {Path}", path);
        return PhotoResult.Ok(path);
    }

    public void DeletePhoto(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            // страховка: видаляємо лише файли з каталогу чеків
            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(ReceiptsDirectory);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return;
            if (File.Exists(full)) File.Delete(full);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не вдалося видалити файл чека {Path}", path);
        }
    }

    // ---------------- Геолокація ----------------

    public async Task<LocationResult> GetCurrentLocationAsync()
    {
        try
        {
            var status = await EnsurePermissionAsync<Permissions.LocationWhenInUse>("Геолокація");
            if (status != PermissionStatus.Granted)
                return LocationResult.Fail("Немає дозволу на геолокацію. Дозвольте доступ у налаштуваннях пристрою.");

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            var location = await Geolocation.Default.GetLocationAsync(request)
                           ?? await Geolocation.Default.GetLastKnownLocationAsync();

            if (location is null)
            {
                logger.LogWarning("Геолокація не повернула координат");
                return LocationResult.Fail("Не вдалося визначити місцезнаходження. Спробуйте ще раз.");
            }

            logger.LogInformation("Визначено координати: {Lat}, {Lon}", location.Latitude, location.Longitude);
            return LocationResult.Ok(location.Latitude, location.Longitude);
        }
        catch (FeatureNotSupportedException ex)
        {
            logger.LogWarning(ex, "Геолокація не підтримується");
            return LocationResult.Fail("Геолокація не підтримується на цьому пристрої.");
        }
        catch (FeatureNotEnabledException ex)
        {
            logger.LogWarning(ex, "Служба геолокації вимкнена");
            return LocationResult.Fail("Увімкніть службу геолокації на пристрої.");
        }
        catch (PermissionException ex)
        {
            logger.LogWarning(ex, "Відмовлено в дозволі на геолокацію");
            return LocationResult.Fail("Немає дозволу на геолокацію.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не вдалося визначити місцезнаходження");
            return LocationResult.Fail("Не вдалося визначити місцезнаходження. Спробуйте ще раз.");
        }
    }

    // ---------------- Дозволи ----------------

    private async Task<PermissionStatus> EnsurePermissionAsync<TPermission>(string name)
        where TPermission : Permissions.BasePermission, new()
    {
        var status = await MainThread.InvokeOnMainThreadAsync(() => Permissions.CheckStatusAsync<TPermission>());
        if (status == PermissionStatus.Granted) return status;

        // iOS не показує діалог повторно після відмови
        if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
        {
            logger.LogWarning("Дозвіл «{Permission}» раніше відхилено (iOS)", name);
            return status;
        }

        status = await MainThread.InvokeOnMainThreadAsync(() => Permissions.RequestAsync<TPermission>());
        if (status != PermissionStatus.Granted)
            logger.LogWarning("Користувач відмовив у дозволі «{Permission}» (статус {Status})", name, status);
        else
            logger.LogInformation("Отримано дозвіл «{Permission}»", name);

        return status;
    }
}
