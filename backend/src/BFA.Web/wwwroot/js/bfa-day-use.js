(function () {
    "use strict";

    function iniciar(root) {
        root.querySelectorAll("[data-day-use-delete]").forEach(function (button) {
            button.addEventListener("click", function () {
                if (!window.BfaConfirm) return;
                var escapeHtml = function (value) {
                    var element = document.createElement("span");
                    element.textContent = value || "";
                    return element.innerHTML;
                };
                var descricao = escapeHtml(button.dataset.participante)
                    + "<br>" + escapeHtml(button.dataset.data)
                    + "<br>" + escapeHtml(button.dataset.valor)
                    + "<br><br>Esta ação não poderá ser desfeita.";
                window.BfaConfirm.confirm({
                    titulo: "Excluir Day Use?",
                    mensagem: "Você está prestes a excluir este registro:",
                    descricao: descricao,
                    textoConfirmar: "Excluir",
                    variante: "danger"
                }).then(function (confirmado) {
                    if (confirmado) button.closest("form").submit();
                });
            });
        });

        root.querySelectorAll("form").forEach(function (form) {
            form.addEventListener("submit", function () {
                form.querySelectorAll('[data-bfa-number="money"]').forEach(function (input) {
                    var valorPtBr = input.value.trim();
                    if (!valorPtBr) return;

                    var valorNumerico = Number(valorPtBr.replace(/\./g, "").replace(",", "."));
                    if (Number.isFinite(valorNumerico)) input.value = valorNumerico.toFixed(2);
                });
            });
        });

        var dialog = root.querySelector("[data-day-use-dialog]");
        if (!dialog) return;

        var typeValue = dialog.querySelector("[data-day-use-type-value]");
        var buttons = dialog.querySelectorAll("[data-day-use-type]");
        var alunoFields = dialog.querySelectorAll("[data-day-use-aluno]");
        var avulsoFields = dialog.querySelectorAll("[data-day-use-avulso]");

        function syncType(type) {
            typeValue.value = type;
            buttons.forEach(function (button) {
                var active = button.dataset.dayUseType === type;
                button.classList.toggle("is-active", active);
                button.setAttribute("aria-pressed", active ? "true" : "false");
            });
            alunoFields.forEach(function (field) { field.hidden = type !== "Aluno"; });
            avulsoFields.forEach(function (field) { field.hidden = type !== "Avulso"; });
        }

        buttons.forEach(function (button) {
            button.addEventListener("click", function () { syncType(button.dataset.dayUseType); });
        });
        syncType(typeValue.value || "Aluno");

        root.querySelectorAll("[data-day-use-open]").forEach(function (button) {
            button.addEventListener("click", function () { dialog.showModal(); });
        });
        dialog.querySelectorAll("[data-day-use-close]").forEach(function (button) {
            button.addEventListener("click", function () { dialog.close(); });
        });
        dialog.addEventListener("click", function (event) {
            if (event.target === dialog) dialog.close();
        });
        if (dialog.hasAttribute("data-day-use-open-on-load")) dialog.showModal();

        var configDialog = root.querySelector("[data-day-use-config-dialog]");
        if (configDialog) {
            root.querySelectorAll("[data-day-use-config-open]").forEach(function (button) {
                button.addEventListener("click", function () { configDialog.showModal(); });
            });
            configDialog.querySelectorAll("[data-day-use-config-close]").forEach(function (button) {
                button.addEventListener("click", function () { configDialog.close(); });
            });
            configDialog.addEventListener("click", function (event) {
                if (event.target === configDialog) configDialog.close();
            });
        }
    }

    document.querySelectorAll("[data-day-use-root]").forEach(iniciar);
})();
