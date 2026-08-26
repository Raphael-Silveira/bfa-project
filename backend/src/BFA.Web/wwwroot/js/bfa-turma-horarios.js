(() => {
    "use strict";

    const lista = document.querySelector("[data-bfa-schedule-list]");
    const modelo = document.querySelector("[data-bfa-schedule-template]");
    const adicionar = document.querySelector("[data-bfa-schedule-add]");
    if (!lista || !modelo || !adicionar) return;

    const formatarHora = (valor) => {
        let digitos = valor.replace(/\D/g, "").slice(0, 4);
        if (digitos.length > 0 && Number(digitos[0]) > 2) {
            digitos = `0${digitos}`.slice(0, 4);
        }
        return digitos.length <= 2
            ? digitos
            : `${digitos.slice(0, 2)}:${digitos.slice(2)}`;
    };

    const iniciarMascaraHora = (raiz) => {
        raiz.querySelectorAll("[data-bfa-time-input]").forEach((input) => {
            if (input.dataset.bfaTimeReady === "true") return;
            input.dataset.bfaTimeReady = "true";
            input.addEventListener("input", () => {
                input.value = formatarHora(input.value);
            });
            input.value = formatarHora(input.value);
        });
    };

    const renumerar = () => {
        const linhas = lista.querySelectorAll("[data-bfa-schedule-row]");
        linhas.forEach((linha, indice) => {
            linha.querySelectorAll("[name]").forEach((campo) => {
                campo.name = campo.name.replace(/Horarios\[\d+\]/, `Horarios[${indice}]`);
            });
            linha.querySelectorAll("[id]").forEach((campo) => {
                campo.id = campo.id.replace(/Horarios_\d+__/, `Horarios_${indice}__`);
            });
            linha.querySelectorAll("label[for]").forEach((label) => {
                label.htmlFor = label.htmlFor.replace(/Horarios_\d+__/, `Horarios_${indice}__`);
            });
        });
        lista.querySelectorAll("[data-bfa-schedule-remove]").forEach((botao) => {
            botao.disabled = linhas.length <= 1;
        });
    };

    adicionar.addEventListener("click", () => {
        const indice = lista.querySelectorAll("[data-bfa-schedule-row]").length;
        const fragmento = modelo.content.cloneNode(true);
        const linha = fragmento.querySelector("[data-bfa-schedule-row]");
        linha.innerHTML = linha.innerHTML.replaceAll("__index__", indice.toString());
        lista.appendChild(fragmento);
        window.BfaDateField?.iniciar(linha);
        iniciarMascaraHora(linha);
        renumerar();
        linha.querySelector("select")?.focus();
    });

    lista.addEventListener("click", (evento) => {
        const botao = evento.target.closest("[data-bfa-schedule-remove]");
        if (!botao || lista.children.length <= 1) return;
        botao.closest("[data-bfa-schedule-row]")?.remove();
        renumerar();
    });

    iniciarMascaraHora(lista);
    renumerar();
})();
