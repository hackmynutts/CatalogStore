using CatalogStore.UI.Handlers;
using CatalogStore.UI.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogStore.UI.Controllers
{
    [Authorize(Roles = "Admin,AdminIT")]
    public class UserController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public UserController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Index()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/User");

            if (!response.IsSuccessStatusCode)
                return View(new List<UserViewModel>());

            var users = await response.Content.ReadFromJsonAsync<List<UserViewModel>>();
            return View(users ?? new List<UserViewModel>());
        }
        public async Task<IActionResult> EditPartial(Guid id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/User/{id}");

            if (!response.IsSuccessStatusCode)
                return NotFound();

            var user = await response.Content.ReadFromJsonAsync<UserViewModel>();
            if (user == null)
                return NotFound();

            return PartialView("_EditUserPartial", user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, UserViewModel model)
        {
            var updateDto = new UpdateUserViewModel
            {
                FullName = model.FullName,
                Email = model.Email,
                SendNotifications = model.SendNotifications
            };

            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PutAsJsonAsync($"api/User/{id}", updateDto);

            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, message = "No se pudo actualizar el usuario." });

            return Json(new { success = true });
        }

        public IActionResult CreatePartial()
        {
            return PartialView("_CreateUserPartial", new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RegisterViewModel model)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsJsonAsync("api/User/register", model);
            return await LinkResultAsync(response, "No se pudo crear el usuario.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(Guid id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsync($"api/User/{id}/reset-password", null);
            return await LinkResultAsync(response, "No se pudo restablecer la contraseña.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendInvitation(Guid id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsync($"api/User/{id}/resend-invitation", null);
            return await LinkResultAsync(response, "No se pudo reenviar la invitación.");
        }

        // Crear, restablecer y reenviar responden igual: a qué correo salió el enlace o, con el correo
        // deshabilitado, el enlace para compartirlo a mano.
        private async Task<IActionResult> LinkResultAsync(HttpResponseMessage response, string fallback)
        {
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, message = await ApiErrorReader.ReadAsync(response, fallback) });

            var result = await response.Content.ReadFromJsonAsync<RegisterResponseDTO>();
            return Json(new
            {
                success = true,
                emailSentTo = result?.EmailSentTo,
                manualLink = result?.ManualLink,
                deliveryFailed = result?.DeliveryFailed ?? false
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.DeleteAsync($"api/User/{id}");

            return RedirectToAction(nameof(Index));
        }
    }
}
