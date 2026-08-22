(() => {
    "use strict";

    const somenteDigitos = (valor) => valor.replace(/\D/g, "");

    const separarDecimal = (valor) => {
        const permitido = valor.replace(/[^\d.,]/g, "");
        const ultimaVirgula = permitido.lastIndexOf(",");
        const ultimaPonto = permitido.lastIndexOf(".");
        let separador = ultimaVirgula;

        if (separador < 0 && ultimaPonto >= 0) {
            const casas = permitido.length - ultimaPonto - 1;
            separador = casas <= 2 ? ultimaPonto : -1;
        }

        const inteiro = somenteDigitos(separador >= 0
            ? permitido.slice(0, separador)
            : permitido);
        const fracao = separador >= 0
            ? somenteDigitos(permitido.slice(separador + 1)).slice(0, 2)
            : "";

        return {
            inteiro: inteiro || (separador >= 0 ? "0" : ""),
            fracao,
            possuiSeparador: separador >= 0
        };
    };

    const removerZerosIniciais = (valor) => valor.replace(/^0+(?=\d)/, "");
    const agruparMilhares = (valor) => removerZerosIniciais(valor)
        .replace(/\B(?=(\d{3})+(?!\d))/g, ".");

    const formatarDecimal = (valor, moeda, completarCasas = false) => {
        const partes = separarDecimal(valor);

        if (!partes.inteiro && !partes.possuiSeparador) {
            return "";
        }

        const inteiro = moeda
            ? agruparMilhares(partes.inteiro)
            : removerZerosIniciais(partes.inteiro);
        const fracao = completarCasas
            ? partes.fracao.padEnd(2, "0")
            : partes.fracao;

        return partes.possuiSeparador || completarCasas
            ? `${inteiro || "0"},${fracao}`
            : inteiro;
    };

    const obterNumero = (valor) => {
        if (!valor) {
            return null;
        }

        const normalizado = valor.replace(/\./g, "").replace(",", ".");
        const numero = Number(normalizado);
        return Number.isFinite(numero) ? numero : null;
    };

    const atualizarValidacao = (input) => {
        if (!window.jQuery?.validator) {
            return;
        }

        const campo = window.jQuery(input);
        if (campo.closest("form").data("validator")) {
            campo.valid();
        }
    };

    const instalarValidacaoPtBr = () => {
        if (!window.jQuery?.validator || window.jQuery.validator.methods.bfaNumeroPtBr) {
            return;
        }

        window.jQuery.validator.addMethod("bfaNumeroPtBr", function (valor, elemento) {
            if (this.optional(elemento)) {
                return true;
            }

            return obterNumero(valor) !== null;
        }, "Informe um número válido.");

        window.jQuery.validator.addMethod("bfaIntervaloPtBr", function (valor, elemento, limites) {
            if (this.optional(elemento)) {
                return true;
            }

            const numero = obterNumero(valor);
            return numero !== null
                && (limites[0] === null || numero >= limites[0])
                && (limites[1] === null || numero <= limites[1]);
        }, "Informe um valor dentro do intervalo permitido.");
    };

    const adicionarRegras = (input) => {
        if (!window.jQuery?.validator) {
            return;
        }

        const campo = window.jQuery(input);
        if (!campo.closest("form").data("validator")) {
            return;
        }

        const minimo = input.dataset.bfaNumberMin;
        const maximo = input.dataset.bfaNumberMax;
        const regras = { bfaNumeroPtBr: true };

        if (minimo !== undefined || maximo !== undefined) {
            regras.bfaIntervaloPtBr = [
                minimo === undefined ? null : Number(minimo),
                maximo === undefined ? null : Number(maximo)
            ];
        }

        campo.rules("add", regras);
    };

    const aplicarMascara = (input, completarCasas = false) => {
        const tipo = input.dataset.bfaNumber;
        let valor;

        if (tipo === "integer") {
            valor = somenteDigitos(input.value).slice(0, 2);
        } else {
            valor = formatarDecimal(input.value, tipo === "money", completarCasas);
        }

        if (input.value !== valor) {
            input.value = valor;
        }
    };

    const iniciarCampo = (input) => {
        aplicarMascara(input, input.value.length > 0);
        adicionarRegras(input);

        input.addEventListener("input", () => {
            aplicarMascara(input);

            if (input.classList.contains("input-validation-error")) {
                atualizarValidacao(input);
            }
        });

        input.addEventListener("blur", () => {
            aplicarMascara(input, input.dataset.bfaNumber !== "integer" && input.value.length > 0);
            atualizarValidacao(input);
        });

        input.addEventListener("change", () => {
            aplicarMascara(input, input.dataset.bfaNumber !== "integer" && input.value.length > 0);
            atualizarValidacao(input);
        });
    };

    const iniciar = () => {
        instalarValidacaoPtBr();
        document.querySelectorAll("[data-bfa-number]").forEach(iniciarCampo);
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", iniciar, { once: true });
    } else {
        iniciar();
    }
})();
