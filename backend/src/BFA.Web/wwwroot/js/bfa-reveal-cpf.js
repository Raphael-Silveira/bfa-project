(function () {
    "use strict";

    document.querySelectorAll("[data-cpf-reveal-button]").forEach(function (button) {
        var value = button.closest("[data-cpf-reveal]").querySelector("[data-cpf-value]");
        var error = button.closest("[data-cpf-reveal]").querySelector("[data-cpf-error]");
        var masked = value.textContent;
        var endpoint = button.dataset.cpfEndpoint;
        var loadedCpf = null;
        var revealed = false;

        button.addEventListener("click", async function () {
            error.hidden = true;

            if (revealed) {
                value.textContent = masked;
                revealed = false;
                button.setAttribute("aria-label", "Mostrar CPF");
                button.setAttribute("title", "Mostrar CPF");
                button.setAttribute("aria-pressed", "false");
                button.querySelector('[data-cpf-eye="visible"]').hidden = false;
                button.querySelector('[data-cpf-eye="hidden"]').hidden = true;
                return;
            }

            if (!loadedCpf) {
                try {
                    var response = await fetch(endpoint, {
                        method: "GET",
                        headers: { "Accept": "application/json" },
                        credentials: "same-origin"
                    });
                    if (!response.ok) throw new Error("cpf-request-failed");
                    var payload = await response.json();
                    if (!payload.cpf) throw new Error("cpf-response-invalid");
                    loadedCpf = payload.cpf;
                } catch (_) {
                    error.hidden = false;
                    return;
                }
            }

            value.textContent = loadedCpf;
            revealed = true;
            button.setAttribute("aria-label", "Ocultar CPF");
            button.setAttribute("title", "Ocultar CPF");
            button.setAttribute("aria-pressed", "true");
            button.querySelector('[data-cpf-eye="visible"]').hidden = true;
            button.querySelector('[data-cpf-eye="hidden"]').hidden = false;
        });
    });
})();
