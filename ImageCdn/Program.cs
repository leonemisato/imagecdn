using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;
using ImageCdn.Models;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = new PhysicalFileProvider(
                            Path.Combine(Directory.GetCurrentDirectory(), @"Images")),
    RequestPath = new PathString("/Images"),
    ServeUnknownFileTypes = true
});

app.MapPost("/api/base64toimg", async context =>
{
    context.Response.ContentType = "application/json";
    try
    {
        var request = await JsonSerializer.DeserializeAsync<JsonRequest>(context.Request.Body);
        var imageContent = request.Base64String;
        var requestWidth = request.Width ?? 0;
        var requestHeight = request.Height ?? 0;
        var fileExtension = imageContent[..1];
        fileExtension = fileExtension switch
        {
            "/" => ".jpg",
            "i" => ".png",
            "R" => ".gif",
            "U" => ".webp",
            _ => ".png"
        };

        var imageBytes = Convert.FromBase64String(imageContent);
        var fileName = Guid.NewGuid().ToString() + fileExtension;
        var imageFolder = @"Images";
        if (!Directory.Exists(imageFolder))
        {
            Directory.CreateDirectory(imageFolder);
        }
        var imagePath = Path.Combine(imageFolder, fileName);

        using (var stream = new MemoryStream(imageBytes))
        {
            using (var originalImage = Image.FromStream(stream))
            {
                int originalWidth = originalImage.Width;
                int originalHeight = originalImage.Height;

                if (requestWidth <= 0 && requestHeight <= 0)
                {
                    requestWidth = originalWidth;
                    requestHeight = originalHeight;
                }

                else if (requestWidth <= 0)
                {
                    requestWidth = (int)Math.Round(requestHeight * (double)originalWidth / originalHeight);
                }

                else if (requestHeight <= 0)
                {
                    requestHeight = (int)Math.Round(requestWidth * (double)originalHeight / originalWidth);
                }

                if (requestHeight < originalHeight || requestWidth < originalWidth)
                {
                    using (var resizedImage = new Bitmap(requestWidth, requestHeight))
                    {
                        using (var graphic = Graphics.FromImage(resizedImage))
                        {
                            graphic.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphic.SmoothingMode = SmoothingMode.HighQuality;
                            graphic.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphic.CompositingQuality = CompositingQuality.HighQuality;
                            graphic.Clear(Color.Transparent);
                            graphic.DrawImage(originalImage, new Rectangle(0, 0, requestWidth, requestHeight));
                            resizedImage.Save(imagePath, originalImage.RawFormat);
                        }

                        imageBytes = File.ReadAllBytes(imagePath);
                    }
                }
                else
                {
                    imageBytes = Convert.FromBase64String(imageContent);
                }
            }
        }
        await File.WriteAllBytesAsync(imagePath, imageBytes);

        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host.Value}";

        if (app.Environment.IsProduction())
            baseUrl += "/ImagesCDN";

        var imageUrl = $"{baseUrl}/{imagePath.Replace('\\', '/')}";
        
        context.Response.StatusCode = 201;
        await JsonSerializer.SerializeAsync(context.Response.Body, new { filePath = imageUrl });
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 400;
        await JsonSerializer.SerializeAsync(context.Response.Body, new { error = $"Erro ao salvar a imagem: {ex.Message}" });
    }
});

app.UseHttpsRedirection();

app.Run();