(() => {
    "use strict";

    const somenteDigitos = (valor) => valor.replace(/\D/g, "");

    const normalizarCnpj = (valor) => {
        const caracteres = valor.toUpperCase().replace(/[^A-Z0-9]/g, "");
        const base = caracteres.slice(0, 12);
        const digitosVerificadores = caracteres
            .slice(12)
            .replace(/\D/g, "")
            .slice(0, 2);
        return `${base}${digitosVerificadores}`;
    };

    const formatarCpf = (valor) => {
        const digitos = somenteDigitos(valor).slice(0, 11);

        if (digitos.length <= 3) {
            return digitos;
        }

        if (digitos.length <= 6) {
            return `${digitos.slice(0, 3)}.${digitos.slice(3)}`;
        }

        if (digitos.length <= 9) {
            return `${digitos.slice(0, 3)}.${digitos.slice(3, 6)}.${digitos.slice(6)}`;
        }

        return `${digitos.slice(0, 3)}.${digitos.slice(3, 6)}.${digitos.slice(6, 9)}-${digitos.slice(9)}`;
    };

    const formatarCnpj = (valor) => {
        const caracteres = normalizarCnpj(valor);

        if (caracteres.length <= 2) {
            return caracteres;
        }

        if (caracteres.length <= 5) {
            return `${caracteres.slice(0, 2)}.${caracteres.slice(2)}`;
        }

        if (caracteres.length <= 8) {
            return `${caracteres.slice(0, 2)}.${caracteres.slice(2, 5)}.${caracteres.slice(5)}`;
        }

        if (caracteres.length <= 12) {
            return `${caracteres.slice(0, 2)}.${caracteres.slice(2, 5)}.${caracteres.slice(5, 8)}/${caracteres.slice(8)}`;
        }

        return `${caracteres.slice(0, 2)}.${caracteres.slice(2, 5)}.${caracteres.slice(5, 8)}/${caracteres.slice(8, 12)}-${caracteres.slice(12)}`;
    };

    const formatarTelefone = (valor) => {
        const digitos = somenteDigitos(valor).slice(0, 11);

        if (digitos.length === 0) {
            return "";
        }

        if (digitos.length <= 2) {
            return `(${digitos}`;
        }

        const ddd = digitos.slice(0, 2);
        const numero = digitos.slice(2);

        if (numero.length <= 4) {
            return `(${ddd}) ${numero}`;
        }

        const tamanhoPrefixo = digitos.length === 11 ? 5 : 4;
        return `(${ddd}) ${numero.slice(0, tamanhoPrefixo)}-${numero.slice(tamanhoPrefixo)}`;
    };

    const formatarCep = (valor) => {
        const digitos = somenteDigitos(valor).slice(0, 8);
        return digitos.length <= 5
            ? digitos
            : `${digitos.slice(0, 5)}-${digitos.slice(5)}`;
    };

    const obterTipoDocumento = (input) => {
        const seletor = input.dataset.bfaDocumentTypeTarget;
        const tipoPessoa = seletor ? document.querySelector(seletor) : null;
        return tipoPessoa?.selectedOptions[0]?.dataset.bfaDocumentType === "cnpj"
            ? "cnpj"
            : "cpf";
    };

    const obterFormatador = (input) => {
        switch (input.dataset.bfaMask) {
            case "phone":
                return formatarTelefone;
            case "cep":
                return formatarCep;
            case "document":
                return obterTipoDocumento(input) === "cnpj" ? formatarCnpj : formatarCpf;
            default:
                return (valor) => valor;
        }
    };

    const obterCaracteresDaMascara = (input, valor) => {
        return input.dataset.bfaMask === "document" && obterTipoDocumento(input) === "cnpj"
            ? normalizarCnpj(valor)
            : somenteDigitos(valor);
    };

    const obterPosicaoPorQuantidadeDeCaracteres = (input, valor, quantidade) => {
        if (quantidade === 0) {
            return 0;
        }

        let encontrados = 0;
        const cnpj = input.dataset.bfaMask === "document"
            && obterTipoDocumento(input) === "cnpj";

        for (let indice = 0; indice < valor.length; indice += 1) {
            if (cnpj ? /[A-Z0-9]/i.test(valor[indice]) : /\d/.test(valor[indice])) {
                encontrados += 1;
            }

            if (encontrados === quantidade) {
                return indice + 1;
            }
        }

        return valor.length;
    };

    const aplicarMascara = (input) => {
        const inicioSelecao = input.selectionStart ?? input.value.length;
        const caracteresAntesDoCursor = obterCaracteresDaMascara(
            input,
            input.value.slice(0, inicioSelecao)).length;
        input.value = obterFormatador(input)(input.value);

        if (document.activeElement === input) {
            const novaPosicao = obterPosicaoPorQuantidadeDeCaracteres(
                input,
                input.value,
                caracteresAntesDoCursor);
            input.setSelectionRange(novaPosicao, novaPosicao);
        }
    };

    const atualizarDocumento = (input, tipoAnterior) => {
        const tipoAtual = obterTipoDocumento(input);

        if (tipoAnterior
            && tipoAnterior !== tipoAtual
            && obterCaracteresDaMascara(input, input.value).length > 0) {
            input.value = "";
        }

        input.placeholder = tipoAtual === "cnpj"
            ? "XX.XXX.XXX/XXXX-00"
            : "000.000.000-00";
        input.inputMode = tipoAtual === "cnpj" ? "text" : "numeric";
        aplicarMascara(input);
        return tipoAtual;
    };

    const iniciarMascara = (input) => {
        input.addEventListener("input", () => aplicarMascara(input));

        if (input.dataset.bfaMask !== "document") {
            aplicarMascara(input);
            return;
        }

        const seletor = input.dataset.bfaDocumentTypeTarget;
        const tipoPessoa = seletor ? document.querySelector(seletor) : null;
        let tipoAnterior = atualizarDocumento(input);

        tipoPessoa?.addEventListener("change", () => {
            tipoAnterior = atualizarDocumento(input, tipoAnterior);
        });
    };

    const iniciar = () => {
        document.querySelectorAll("[data-bfa-mask]").forEach(iniciarMascara);
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", iniciar, { once: true });
    } else {
        iniciar();
    }
})();
