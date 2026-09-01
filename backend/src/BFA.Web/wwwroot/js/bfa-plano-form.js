(() => {
    "use strict";

    const nomesDuracao = new Map([
        [1, "Mensal"],
        [3, "Trimestral"],
        [6, "Semestral"],
        [12, "Anual"]
    ]);

    const iniciar = (form) => {
        const duracao = form.querySelector("[data-bfa-plan-duration]");
        const rotuloDuracao = form.querySelector("[data-bfa-plan-duration-label]");
        const taxa = form.querySelector("[data-bfa-plan-fee]");
        const valorTaxa = taxa?.querySelector("input");
        const escolhas = [...form.querySelectorAll("[data-bfa-plan-fee-choice]")];

        const atualizarDuracao = () => {
            if (!duracao || !rotuloDuracao) return;
            const meses = Number(duracao.value);
            rotuloDuracao.textContent = nomesDuracao.get(meses)
                ?? (meses > 0 ? `${meses} meses` : "Informe a duração em meses.");
        };

        const atualizarTaxa = () => {
            if (!taxa || !valorTaxa) return;
            const cobra = escolhas.find((item) => item.checked)?.value === "true";
            taxa.hidden = !cobra;
            valorTaxa.disabled = !cobra;
            valorTaxa.required = cobra;
            taxa.setAttribute("aria-hidden", String(!cobra));
            if (!cobra) valorTaxa.value = "";
        };

        duracao?.addEventListener("input", atualizarDuracao);
        escolhas.forEach((item) => item.addEventListener("change", atualizarTaxa));
        atualizarDuracao();
        atualizarTaxa();
    };

    const executar = () => document.querySelectorAll("[data-bfa-plan-form]").forEach(iniciar);
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", executar, { once: true });
    } else {
        executar();
    }
})();
