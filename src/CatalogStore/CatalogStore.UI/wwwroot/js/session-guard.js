// Guardián de sesión para todas las llamadas fetch de la UI.
// Si el servidor responde 401, la sesión expiró (el servidor ya cerró la cookie): se avisa una sola vez y se manda
// al login. Se corta la cadena de la promesa para que el código de cada pantalla no muestre además su propio error.
(function () {
    const originalFetch = window.fetch;
    let redirecting = false;

    window.fetch = async function (...args) {
        const response = await originalFetch.apply(this, args);

        if (response.status === 401 && !location.pathname.toLowerCase().startsWith('/account/login')) {
            if (!redirecting) {
                redirecting = true;
                const goToLogin = () => { location.href = '/Account/Login?expired=true'; };
                if (window.Swal) {
                    Swal.fire({
                        icon: 'warning',
                        title: 'Tu sesión expiró',
                        text: 'Iniciá sesión de nuevo para continuar.',
                        confirmButtonText: 'Ir a iniciar sesión',
                        allowOutsideClick: false,
                        allowEscapeKey: false
                    }).then(goToLogin);
                } else {
                    goToLogin();
                }
            }
            return new Promise(() => { });
        }

        return response;
    };
})();
