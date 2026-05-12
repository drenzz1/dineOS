# DineOS — File Uploads (M3.9)

`POST /api/v1/menu/items/{id}/image` is the first upload endpoint in the backend.  
It accepts a multipart image file, stores it under a configurable root directory with a
UUID-based filename, updates `MenuItem.ImageUrl`, and returns the public URL to the caller.

## Where the code lives

| Concern | File |
|---|---|
| Storage contract | [`src/DineOS.Application/Interfaces/Services/IFileStorageService.cs`](../../backend/src/DineOS.Application/Interfaces/Services/IFileStorageService.cs) |
| Local filesystem implementation | [`src/DineOS.Infrastructure/Services/LocalFileStorageService.cs`](../../backend/src/DineOS.Infrastructure/Services/LocalFileStorageService.cs) |
| Configuration options | [`src/DineOS.Application/Options/FileStorageOptions.cs`](../../backend/src/DineOS.Application/Options/FileStorageOptions.cs) |
| Validation rules | [`src/DineOS.Application/Menu/UploadMenuItemImageRequest.cs`](../../backend/src/DineOS.Application/Menu/UploadMenuItemImageRequest.cs) |
| Service method | [`src/DineOS.Infrastructure/Services/MenuService.cs`](../../backend/src/DineOS.Infrastructure/Services/MenuService.cs) — `UploadMenuItemImageAsync` |
| Controller action | [`src/DineOS.Api/Controllers/MenuController.cs`](../../backend/src/DineOS.Api/Controllers/MenuController.cs) — `POST items/{id}/image` |
| Unit tests (storage) | [`tests/DineOS.Tests/Unit/LocalFileStorageServiceTests.cs`](../../backend/tests/DineOS.Tests/Unit/LocalFileStorageServiceTests.cs) |
| Integration tests (endpoint) | [`tests/DineOS.Tests/Integration/MenuItemImageUploadTests.cs`](../../backend/tests/DineOS.Tests/Integration/MenuItemImageUploadTests.cs) |

## Configuration

All keys live under the `FileStorage` section.

| Key | Default (production) | Development override | Description |
|---|---|---|---|
| `FileStorage:RootPath` | `/app/uploads` | `uploads` (relative) | Absolute or relative path where files are written. Relative paths resolve against the process working directory. |
| `FileStorage:UrlBasePath` | `/uploads` | _(same)_ | URL prefix prepended to every returned path. Must match the `RequestPath` used by the static-files middleware. |
| `FileStorage:MaxBytes` | `5242880` (5 MB) | _(same)_ | Maximum accepted file size in bytes. |
| `FileStorage:ServeLocally` | `false` | _(same)_ | Set to `true` to enable static-file serving outside the `Development` environment. |

In `appsettings.Development.json` `RootPath` is `"uploads"` (relative). With `ASPNETCORE_ENVIRONMENT=Development` and `WORKDIR /app` in Docker, `Path.GetFullPath("uploads")` resolves to `./uploads` on the developer's machine and `/app/uploads` inside the container — the same physical path the named Docker volume is mounted to.

### docker-compose

The API container mounts a named volume:

```yaml
services:
  api:
    volumes:
      - dineos_uploads:/app/uploads

volumes:
  dineos_uploads:
```

Uploaded files survive container restarts. Remove the volume with `docker volume rm backend_dineos_uploads` to purge all uploads in a local environment.

## Endpoint

```
POST /api/v1/menu/items/{id}/image
Content-Type: multipart/form-data
Authorization: Bearer <token>   (Manager or above)
```

Form field name: **`image`**

### Success response — 200

```json
{
  "success": true,
  "data": {
    "imageUrl": "/uploads/menu-items/3f1a2b4c5d6e7f8a9b0c1d2e3f4a5b6c.png"
  }
}
```

The returned `imageUrl` is a root-relative URL that the frontend can append to the API base to build a full image URL for an `<img>` tag.

### Example — curl

```bash
curl -s -X POST http://localhost:5000/api/v1/menu/items/42/image \
  -H "Authorization: Bearer $TOKEN" \
  -F "image=@/path/to/photo.png;type=image/png"
```

## File storage path

`LocalFileStorageService` writes files to:

```
{RootPath}/{subfolder}/{guid}{ext}
```

For the menu-item endpoint, `subfolder` is always `menu-items`:

```
/app/uploads/menu-items/3f1a2b4c5d6e7f8a9b0c1d2e3f4a5b6c.png
```

The corresponding URL returned to the client is:

```
/uploads/menu-items/3f1a2b4c5d6e7f8a9b0c1d2e3f4a5b6c.png
```

## Filename strategy

The original filename supplied by the client is **never used** as the stored filename. Only the file extension is extracted (lowercased), and a new UUID-based name is generated:

```csharp
var ext    = Path.GetExtension(originalFileName).ToLowerInvariant(); // ".png"
var stored = $"{Guid.NewGuid():N}{ext}";                             // "3f1a2b4c...6c.png"
```

Benefits:
- **No collisions** — two uploads with the same name never overwrite each other.
- **Path-traversal prevention** — a filename like `../../etc/passwd` contributes only `.` (no valid extension in that case) or its extension to the stored name; the path is never used directly.
- **No PII leakage** — the original display name is not persisted to the filesystem.

## Path-traversal guard

After resolving the target directory with `Path.GetFullPath`, `LocalFileStorageService` asserts the result is inside the configured root:

```csharp
if (!resolvedPath.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)
    && resolvedPath != _rootPath)
    throw new InvalidOperationException("Resolved path is outside the configured storage root.");
```

This guard fires on both write (`SaveAsync`) and delete (`DeleteAsync`), preventing a crafted subfolder or URL from escaping the storage root.

## Validation rules

Validated by `UploadMenuItemImageRequestValidator` before any file is written.  
Failures return **HTTP 400** with RFC 7807 `ValidationProblemDetails`.

| Error code | Condition |
|---|---|
| `FILE_EMPTY` | `Content-Length` is 0 |
| `FILE_TOO_LARGE` | File exceeds `FileStorage:MaxBytes` (default 5 MB) |
| `UNSUPPORTED_CONTENT_TYPE` | `Content-Type` is not `image/jpeg`, `image/png`, or `image/webp` |
| `INVALID_EXTENSION` | Extension is not `.jpg`, `.jpeg`, `.png`, or `.webp` |
| `EXTENSION_MISMATCH` | Extension does not match the declared `Content-Type` (e.g. `.png` with `image/jpeg`) |

### 400 example

```json
{
  "title": "Validation failed.",
  "status": 400,
  "errors": {
    "FILE_TOO_LARGE": ["File must not exceed 5 MB."]
  }
}
```

## Static-file serving in development

When `ASPNETCORE_ENVIRONMENT=Development` (or `FileStorage:ServeLocally=true`), `Program.cs` mounts the uploads directory as a static-file provider:

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider  = new PhysicalFileProvider(uploadsRoot),
    RequestPath   = fileStorageOpts.UrlBasePath,      // "/uploads"
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.CacheControl = "public, max-age=3600",
});
```

Only four `Content-Type` values are served (all others return 404):

| Extension | Content-Type |
|---|---|
| `.jpg` / `.jpeg` | `image/jpeg` |
| `.png` | `image/png` |
| `.webp` | `image/webp` |

In production (`ServeLocally=false`), the `/uploads` path returns 404. Production deployments should serve the uploads volume through a reverse proxy (nginx, Caddy) or object storage.

## Compensating delete on DB failure

After a file is written, the service persists `MenuItem.ImageUrl` to PostgreSQL. If `SaveChangesAsync` throws, the just-written file is deleted to avoid orphaned files:

```csharp
var imageUrl = await fileStorage.SaveAsync(...);
item.ImageUrl = imageUrl;
try { await db.SaveChangesAsync(ct); }
catch (Exception ex)
{
    await fileStorage.DeleteAsync(imageUrl, ct);
    throw;
}
```

## Follow-up items (out of scope for M3.9)

- **Orphaned-file cleanup** — if the API process dies between `SaveAsync` and `SaveChangesAsync`, the file on disk has no corresponding DB record. A periodic background job (or startup scan) comparing `MenuItem.ImageUrl` values against the filesystem would reclaim these. Not implemented.
- **Old image eviction** — replacing a menu item image leaves the previous file on disk. The service does not delete the old URL. A future improvement would read `item.ImageUrl` before the upload and call `DeleteAsync` on it after the DB commit succeeds.
- **Object storage (S3 / Azure Blob)** — swap `LocalFileStorageService` for a cloud-backed `IFileStorageService` implementation. The interface, validator, and service layer require no changes.
- **Image resizing / thumbnail generation** — post-upload processing pipeline.
