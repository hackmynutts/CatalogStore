using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogStore.UI.Controllers
{
    // robots.txt y sitemap.xml para los buscadores. Se arman con Seo:SiteUrl (la dirección pública) para que tengan siempre
    // la dirección correcta; sin esa configuración (pruebas, desarrollo) el sitio le pide a los buscadores que no lo indexen.
    [AllowAnonymous]
    public class SeoController : Controller
    {
        // Páginas públicas que se ofrecen a los buscadores.
        private static readonly string[] PublicPaths = { "/", "/Home/Catalog" };

        // Áreas internas: requieren sesión, no tiene sentido que los buscadores las visiten.
        private static readonly string[] PrivatePrefixes =
        {
            "/Account/", "/Client/", "/Order/", "/Sales/", "/Product/", "/ProductImage/", "/Inventory/", "/InventoryLine/",
            "/InventoryTransaction/", "/User/", "/Status/", "/Eventlog/", "/Goals/", "/Reports/", "/Vendedor/",
            "/Home/ConsultationPdf", "/Home/Error", "/Home/StatusCode"
        };

        private readonly string _siteUrl;
        public SeoController(IConfiguration configuration)
        {
            _siteUrl = (configuration["Seo:SiteUrl"] ?? string.Empty).Trim().TrimEnd('/');
        }

        [HttpGet("/robots.txt")]
        public ContentResult Robots()
        {
            var text = new StringBuilder();
            text.AppendLine("User-agent: *");
            if (_siteUrl.Length == 0)
            {
                text.AppendLine("Disallow: /");
            }
            else
            {
                text.AppendLine("Allow: /$");
                text.AppendLine("Allow: /Home/Catalog");
                foreach (var prefix in PrivatePrefixes) text.AppendLine($"Disallow: {prefix}");
                text.AppendLine();
                text.AppendLine($"Sitemap: {_siteUrl}/sitemap.xml");
            }
            return Content(text.ToString(), "text/plain", Encoding.UTF8);
        }

        [HttpGet("/sitemap.xml")]
        public IActionResult Sitemap()
        {
            if (_siteUrl.Length == 0) return NotFound();

            var xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
            foreach (var path in PublicPaths)
            {
                xml.AppendLine("  <url>");
                xml.AppendLine($"    <loc>{_siteUrl}{path}</loc>");
                xml.AppendLine("  </url>");
            }
            xml.AppendLine("</urlset>");
            return Content(xml.ToString(), "application/xml", Encoding.UTF8);
        }
    }
}
