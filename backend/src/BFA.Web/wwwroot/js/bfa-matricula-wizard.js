(() => {
    "use strict";

    const normalizarBusca = (valor) => valor.normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .toLowerCase();
    const numeroPtBr = (valor) => {
        const normalizado = (valor ?? "").replace(/\./g, "").replace(",", ".");
        const numero = Number(normalizado);
        return Number.isFinite(numero) ? numero : null;
    };
    const moeda = (valor) => new Intl.NumberFormat("pt-BR", {
        style: "currency", currency: "BRL"
    }).format(valor);
    const analisarData = (valor) => {
        const partes = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec((valor ?? "").trim());
        if (!partes) return null;
        const data = new Date(Number(partes[3]), Number(partes[2]) - 1, Number(partes[1]), 12);
        return data.getFullYear() === Number(partes[3])
            && data.getMonth() === Number(partes[2]) - 1
            && data.getDate() === Number(partes[1]) ? data : null;
    };
    const dataIso = (data) => `${data.getFullYear()}-${String(data.getMonth() + 1).padStart(2, "0")}-${String(data.getDate()).padStart(2, "0")}`;
    const idadeEm = (nascimento, referencia) => {
        let idade = referencia.getFullYear() - nascimento.getFullYear();
        if (referencia.getMonth() < nascimento.getMonth()
            || referencia.getMonth() === nascimento.getMonth()
                && referencia.getDate() < nascimento.getDate()) idade -= 1;
        return idade;
    };
    const mascararCpf = (valor) => {
        const digitos = (valor ?? "").replace(/\D/g, "");
        return digitos.length === 11 ? `***.***.***-${digitos.slice(-2)}` : "Não informado";
    };

    const iniciarWizard = (formulario) => {
        const secoes = [...formulario.querySelectorAll("[data-step]")];
        const indicadores = [...formulario.querySelectorAll("[data-step-indicator]")];
        const erroCliente = document.createElement("p");
        erroCliente.className = "bfa-admin-notice bfa-matricula-client-error";
        erroCliente.setAttribute("role", "alert");
        erroCliente.hidden = true;
        formulario.querySelector(".bfa-matricula-stepper").after(erroCliente);
        let passoAtual = Math.min(5, Math.max(1, Number(formulario.dataset.passoInicial) || 1));
        let maiorPasso = passoAtual;
        let alterado = false;
        let enviando = false;
        let requisicaoPlanos = null;
        let requisicaoHorarios = null;

        const mostrarErro = (mensagem, controle) => {
            erroCliente.textContent = mensagem;
            erroCliente.hidden = false;
            controle?.focus();
            erroCliente.scrollIntoView({ behavior: "smooth", block: "center" });
            return false;
        };
        const limparErro = () => {
            erroCliente.hidden = true;
            erroCliente.textContent = "";
        };
        const irPara = (passo) => {
            passoAtual = Math.min(5, Math.max(1, passo));
            maiorPasso = Math.max(maiorPasso, passoAtual);
            secoes.forEach((secao) => {
                const atual = Number(secao.dataset.step) === passoAtual;
                secao.hidden = !atual;
                secao.setAttribute("aria-hidden", String(!atual));
            });
            indicadores.forEach((item) => {
                const numero = Number(item.dataset.stepIndicator);
                item.classList.toggle("is-complete", numero < passoAtual);
                item.classList.toggle("is-current", numero === passoAtual);
                item.classList.toggle("is-upcoming", numero > passoAtual);
                const botao = item.querySelector("button");
                botao.disabled = numero > maiorPasso;
                botao.toggleAttribute("aria-current", numero === passoAtual);
                botao.querySelector(".bfa-matricula-stepper__state").textContent =
                    numero < passoAtual ? "Concluída" : numero === passoAtual ? "Atual" : "Próxima";
            });
            limparErro();
            if (passoAtual === 5) atualizarRevisao();
            formulario.querySelector(`[data-step="${passoAtual}"] h2`)?.focus({ preventScroll: true });
            formulario.querySelector(".bfa-matricula-stepper")?.scrollIntoView({ behavior: "smooth", block: "start" });
        };

        const modoAluno = () => formulario.querySelector("[data-student-mode]:checked")?.value;
        const alunoSelecionado = () => formulario.querySelector("[data-student-option]:checked");
        const atualizarModoAluno = () => {
            const existente = modoAluno() === "existente";
            formulario.querySelector("[data-student-existing]").hidden = !existente;
            formulario.querySelector("[data-student-new]").hidden = existente;
            formulario.querySelectorAll(".bfa-matricula-mode-card").forEach((card) =>
                card.classList.toggle("is-selected", card.querySelector("input").checked));
            atualizarResponsaveisExistentes();
            atualizarOrientacaoResponsavel();
        };
        const atualizarResponsaveisExistentes = () => {
            const alunoId = alunoSelecionado()?.value;
            const recipiente = formulario.querySelector("[data-existing-guardians]");
            let possui = false;
            recipiente.querySelectorAll("[data-existing-guardians-for]").forEach((lista) => {
                const visivel = lista.dataset.existingGuardiansFor === alunoId;
                lista.hidden = !visivel;
                possui ||= visivel;
            });
            recipiente.hidden = !possui;
        };
        const nascimentoSelecionado = () => {
            if (modoAluno() === "existente") {
                const iso = alunoSelecionado()?.dataset.studentBirthIso;
                return iso ? new Date(`${iso}T12:00:00`) : null;
            }
            return analisarData(formulario.querySelector("#NovoAluno_DataNascimentoTexto")?.value);
        };
        const possuiResponsavelExistente = () => {
            const alunoId = alunoSelecionado()?.value;
            return Boolean(alunoId && formulario.querySelector(
                `[data-existing-guardians-for="${CSS.escape(alunoId)}"]`));
        };
        const atualizarOrientacaoResponsavel = () => {
            const inicio = analisarData(formulario.querySelector("#DataInicioTexto")?.value) ?? new Date();
            const nascimento = nascimentoSelecionado();
            const menor = nascimento && idadeEm(nascimento, inicio) < 18;
            formulario.querySelector("[data-guardian-guidance]").textContent = menor
                ? possuiResponsavelExistente()
                    ? "Este aluno é menor e já possui responsável vinculado. Você também pode adicionar outro."
                    : "Informe pelo menos um responsável."
                : "Responsável não é obrigatório para este aluno.";
        };

        const filtrarAlunos = () => {
            const termo = normalizarBusca(formulario.querySelector("[data-student-search]").value);
            let visiveis = 0;
            formulario.querySelectorAll("[data-student-card]").forEach((card) => {
                const corresponde = normalizarBusca(card.dataset.searchText).includes(termo);
                card.hidden = !corresponde;
                if (corresponde) visiveis += 1;
            });
            formulario.querySelector("[data-student-empty]").hidden = visiveis > 0;
        };

        const reindexarResponsaveis = () => {
            formulario.querySelectorAll("[data-guardian-card]").forEach((card, indice) => {
                card.querySelector("[data-guardian-number]").textContent = String(indice + 1);
                card.querySelectorAll("[name], [id], label[for], [data-valmsg-for]").forEach((elemento) => {
                    for (const atributo of ["name", "id", "for", "data-valmsg-for"]) {
                        const valor = elemento.getAttribute(atributo);
                        if (valor?.match(/Responsaveis(?:_|\[)\d+/))
                            elemento.setAttribute(atributo, valor
                                .replace(/Responsaveis_\d+__/g, `Responsaveis_${indice}__`)
                                .replace(/Responsaveis\[\d+\]/g, `Responsaveis[${indice}]`));
                    }
                });
            });
        };
        const atualizarOutro = (select) => {
            const campo = select.closest("[data-guardian-card]").querySelector("[data-other-relation]");
            campo.hidden = select.value !== "Outro";
            const input = campo.querySelector("input");
            if (campo.hidden) input.value = "";
        };
        const adicionarResponsavel = () => {
            const lista = formulario.querySelector("[data-guardian-list]");
            const indice = lista.querySelectorAll("[data-guardian-card]").length;
            const template = document.querySelector("#bfa-guardian-template");
            const fragmento = template.content.cloneNode(true);
            fragmento.querySelectorAll("*").forEach((item) => {
                for (const atributo of ["name", "id", "for", "data-valmsg-for"]) {
                    const valor = item.getAttribute(atributo);
                    if (valor?.includes("__index__"))
                        item.setAttribute(atributo, valor.replaceAll("__index__", String(indice)));
                }
            });
            lista.append(fragmento);
            const card = lista.lastElementChild;
            window.BfaInputMasks?.iniciar(card);
            card.querySelector("input")?.focus();
            alterado = true;
        };

        const planoSelecionado = () => formulario.querySelector("[data-plan-option]:checked");
        const atualizarCamposPlano = (inicializarValores = false) => {
            const plano = planoSelecionado();
            formulario.querySelectorAll(".bfa-matricula-plan-card").forEach((card) =>
                card.classList.toggle("is-selected", card.querySelector("input").checked));
            if (!plano) return;
            const mensalidade = formulario.querySelector("#ValorMensalContratadoTexto");
            const taxaAtiva = formulario.querySelector("[data-fee-toggle]");
            const taxaValor = formulario.querySelector("[data-fee-value]");
            if (inicializarValores || !mensalidade.value) mensalidade.value = plano.dataset.planPrice;
            if (inicializarValores) {
                taxaAtiva.checked = plano.dataset.planFeeEnabled === "true";
                taxaValor.value = taxaAtiva.checked ? plano.dataset.planFee : "";
            }
            formulario.querySelector("[data-catalog-price]").textContent = moeda(numeroPtBr(plano.dataset.planPrice));
            formulario.querySelector("[data-selected-plan-summary]").textContent =
                `${plano.dataset.planName} · ${plano.dataset.planFrequency}x por semana`;
            formulario.querySelector("[data-grade-plan]").textContent = plano.dataset.planName;
            formulario.querySelector("[data-grade-limit]").textContent =
                `${plano.dataset.planFrequency} horários por semana`;
            atualizarTaxa();
        };
        const atualizarTaxa = () => {
            const ativa = formulario.querySelector("[data-fee-toggle]").checked;
            const valor = formulario.querySelector("[data-fee-value]");
            valor.disabled = !ativa;
            if (!ativa) valor.value = "";
        };
        const criarPlanoCard = (plano, selecionado) => {
            const label = document.createElement("label");
            label.className = "bfa-matricula-plan-card";
            const input = document.createElement("input");
            input.type = "radio";
            input.name = "PlanoVersaoId";
            input.value = plano.planoVersaoId;
            input.checked = plano.planoVersaoId === selecionado;
            input.dataset.planOption = "";
            input.dataset.planName = plano.nome;
            input.dataset.planFrequency = plano.frequenciaSemanal;
            input.dataset.planDuration = plano.duracaoMeses;
            input.dataset.planPrice = plano.valorMensalInput;
            input.dataset.planFeeEnabled = String(plano.cobraMatricula).toLowerCase();
            input.dataset.planFee = plano.valorMatriculaInput;
            const escopo = document.createElement("span");
            escopo.className = "bfa-matricula-plan-card__scope";
            escopo.textContent = plano.escopo;
            const nome = document.createElement("strong"); nome.textContent = plano.nome;
            const dados = document.createElement("span"); dados.textContent = `${plano.frequencia} · ${plano.duracao}`;
            const preco = document.createElement("b"); preco.textContent = `${plano.valorMensal} / mês`;
            const taxa = document.createElement("span"); taxa.textContent = `Taxa de matrícula · ${plano.valorMatricula}`;
            const acao = document.createElement("em"); acao.textContent = "Selecionar";
            label.append(input, escopo, nome, dados, preco, taxa, acao);
            return label;
        };
        const carregarPlanos = async () => {
            const data = formulario.querySelector("#DataInicioTexto").value;
            if (!analisarData(data)) return;
            requisicaoPlanos?.abort();
            requisicaoPlanos = new AbortController();
            const carregando = formulario.querySelector("[data-plan-loading]");
            carregando.hidden = false;
            try {
                const resposta = await fetch(`${formulario.dataset.planosUrl}?dataInicio=${encodeURIComponent(data)}`, {
                    headers: { Accept: "application/json" }, signal: requisicaoPlanos.signal
                });
                if (!resposta.ok) throw new Error("planos");
                const dados = await resposta.json();
                const lista = formulario.querySelector("[data-plan-list]");
                const selecionado = planoSelecionado()?.value;
                lista.replaceChildren(...dados.planos.map((plano) => criarPlanoCard(plano, selecionado)));
                formulario.querySelector("[data-plan-empty]").hidden = dados.planos.length > 0;
                if (!planoSelecionado()) {
                    limparPlanoEGrade();
                } else {
                    atualizarCamposPlano(false);
                    await carregarHorarios();
                }
            } catch (erro) {
                if (erro.name !== "AbortError") mostrarErro("Não foi possível atualizar os planos. Tente novamente.");
            } finally {
                carregando.hidden = true;
            }
        };
        const limparPlanoEGrade = () => {
            formulario.querySelector("#ValorMensalContratadoTexto").value = "";
            formulario.querySelector("[data-fee-toggle]").checked = false;
            formulario.querySelector("[data-fee-value]").value = "";
            formulario.querySelector("[data-end-date]").textContent = "—";
            formulario.querySelector("[data-schedule-list]").replaceChildren();
            atualizarTaxa();
            atualizarGrade();
        };

        const criarHorarioCard = (horario, selecionados) => {
            const label = document.createElement("label");
            label.className = `bfa-matricula-schedule-card${horario.lotado ? " is-full" : ""}`;
            const input = document.createElement("input");
            input.type = "checkbox";
            input.name = "TurmaHorarioIds";
            input.value = horario.turmaHorarioId;
            input.disabled = horario.lotado;
            input.checked = !horario.lotado && selecionados.has(horario.turmaHorarioId);
            input.dataset.scheduleOption = "";
            input.dataset.day = horario.diaSemanaOrdem;
            input.dataset.start = horario.horaInicio;
            input.dataset.end = horario.horaFim;
            input.dataset.className = horario.nomeTurma;
            input.dataset.professor = horario.professor;
            input.dataset.dayName = horario.diaSemana;
            input.dataset.baseUnavailable = String(horario.lotado);
            const turma = document.createElement("strong"); turma.textContent = horario.nomeTurma;
            const hora = document.createElement("b"); hora.textContent = horario.horario;
            const professor = document.createElement("span"); professor.textContent = `Professor · ${horario.professor}`;
            const ocupacao = document.createElement("span"); ocupacao.textContent = `Ocupação · ${horario.ocupacao} / ${horario.capacidade}`;
            const vagas = document.createElement("em");
            vagas.textContent = horario.lotado ? "Lotado" : horario.vagasDisponiveis === 1
                ? "1 vaga disponível" : `${horario.vagasDisponiveis} vagas disponíveis`;
            const acao = document.createElement("small"); acao.textContent = horario.lotado ? "Indisponível" : "Selecionar";
            label.append(input, turma, hora, professor, ocupacao, vagas, acao);
            return label;
        };
        const renderizarHorarios = (horarios) => {
            const lista = formulario.querySelector("[data-schedule-list]");
            const selecionados = new Set([...formulario.querySelectorAll("[data-schedule-option]:checked")].map((item) => item.value));
            const grupos = new Map();
            horarios.forEach((horario) => {
                if (!grupos.has(horario.diaSemanaOrdem)) grupos.set(horario.diaSemanaOrdem, []);
                grupos.get(horario.diaSemanaOrdem).push(horario);
            });
            lista.replaceChildren();
            [...grupos.entries()].sort(([a], [b]) => a - b).forEach(([, itens]) => {
                const secao = document.createElement("section");
                secao.className = "bfa-matricula-schedule-day";
                const titulo = document.createElement("h3"); titulo.textContent = itens[0].diaSemana;
                const grade = document.createElement("div"); grade.className = "bfa-matricula-schedule-grid";
                grade.append(...itens.map((horario) => criarHorarioCard(horario, selecionados)));
                secao.append(titulo, grade);
                lista.append(secao);
            });
            formulario.querySelector("[data-schedule-empty]").hidden = horarios.length > 0;
            atualizarGrade();
        };
        const carregarHorarios = async () => {
            const plano = planoSelecionado();
            const data = formulario.querySelector("#DataInicioTexto").value;
            if (!plano || !analisarData(data)) return;
            requisicaoHorarios?.abort();
            requisicaoHorarios = new AbortController();
            const carregando = formulario.querySelector("[data-schedule-loading]");
            carregando.hidden = false;
            try {
                const url = `${formulario.dataset.horariosUrl}?dataInicio=${encodeURIComponent(data)}&planoVersaoId=${encodeURIComponent(plano.value)}`;
                const resposta = await fetch(url, { headers: { Accept: "application/json" }, signal: requisicaoHorarios.signal });
                const dados = await resposta.json();
                if (!resposta.ok) throw new Error(dados.mensagem ?? "horarios");
                formulario.querySelector("[data-end-date]").textContent = dados.dataFimPrevista;
                renderizarHorarios(dados.horarios);
            } catch (erro) {
                if (erro.name !== "AbortError") mostrarErro(
                    erro.message === "horarios" ? "Não foi possível atualizar os horários." : erro.message);
            } finally {
                carregando.hidden = true;
            }
        };
        const conflita = (primeiro, segundo) => primeiro.dataset.day === segundo.dataset.day
            && primeiro.dataset.start < segundo.dataset.end
            && segundo.dataset.start < primeiro.dataset.end;
        const atualizarGrade = () => {
            const opcoes = [...formulario.querySelectorAll("[data-schedule-option]")];
            const selecionados = opcoes.filter((item) => item.checked);
            const limite = Number(planoSelecionado()?.dataset.planFrequency ?? 0);
            opcoes.forEach((opcao) => {
                const card = opcao.closest(".bfa-matricula-schedule-card");
                const lotado = opcao.dataset.baseUnavailable === "true";
                const conflito = !opcao.checked && selecionados.some((item) => conflita(opcao, item));
                const excede = !opcao.checked && limite > 0 && selecionados.length >= limite;
                opcao.disabled = lotado || conflito || excede;
                card.classList.toggle("is-selected", opcao.checked);
                card.classList.toggle("is-conflict", conflito);
                card.classList.toggle("is-limit", excede && !conflito);
                const acao = card.querySelector("small");
                if (!lotado) acao.textContent = conflito ? "Conflita com a seleção"
                    : excede ? "Limite do plano atingido" : opcao.checked ? "Selecionado" : "Selecionar";
            });
            formulario.querySelector("[data-grade-count]").textContent =
                `${selecionados.length} de ${limite || "—"}`;
        };

        const atualizarRevisao = () => {
            const aluno = formulario.querySelector("[data-review-student]");
            if (modoAluno() === "existente" && alunoSelecionado()) {
                const selecionado = alunoSelecionado();
                aluno.replaceChildren(criarResumo([
                    selecionado.dataset.studentName,
                    selecionado.dataset.studentBirth
                ]));
            } else {
                aluno.replaceChildren(criarResumo([
                    formulario.querySelector("#NovoAluno_NomeCompleto").value,
                    formulario.querySelector("#NovoAluno_DataNascimentoTexto").value,
                    `CPF ${mascararCpf(formulario.querySelector("#NovoAluno_Cpf").value)}`
                ]));
            }

            const responsaveis = formulario.querySelector("[data-review-guardians]");
            responsaveis.replaceChildren();
            const existente = formulario.querySelector(`[data-existing-guardians-for="${CSS.escape(alunoSelecionado()?.value ?? "-")}"]`);
            existente?.querySelectorAll(".bfa-matricula-existing-guardian").forEach((item) =>
                responsaveis.append(item.cloneNode(true)));
            formulario.querySelectorAll("[data-guardian-card]").forEach((card) => {
                const nome = card.querySelector('[name$=".NomeCompleto"]').value;
                if (!nome) return;
                const relacao = card.querySelector('[name$=".TipoRelacao"]').selectedOptions[0]?.textContent;
                const marcadores = [];
                if (card.querySelector("[data-primary-guardian]").checked) marcadores.push("Principal contato");
                if (card.querySelector('[name$=".ResponsavelFinanceiro"]:checked')) marcadores.push("Responsável financeiro");
                responsaveis.append(criarResumo([nome, relacao, ...marcadores]));
            });
            if (!responsaveis.childElementCount) responsaveis.textContent = "Nenhum responsável informado.";

            const plano = planoSelecionado();
            const revisaoPlano = formulario.querySelector("[data-review-plan]");
            if (plano) {
                const catalogo = numeroPtBr(plano.dataset.planPrice);
                const contratado = numeroPtBr(formulario.querySelector("#ValorMensalContratadoTexto").value);
                const itens = [
                    plano.dataset.planName,
                    `${plano.dataset.planFrequency}x por semana · ${plano.dataset.planDuration} meses`,
                    `${formulario.querySelector("#DataInicioTexto").value} a ${formulario.querySelector("[data-end-date]").textContent}`,
                    `Mensalidade contratada · ${moeda(contratado)}`
                ];
                if (catalogo !== contratado) itens.push(`Valor de catálogo · ${moeda(catalogo)}`);
                const taxa = formulario.querySelector("[data-fee-toggle]").checked
                    ? moeda(numeroPtBr(formulario.querySelector("[data-fee-value]").value)) : "Isenta";
                itens.push(`Taxa de matrícula · ${taxa}`);
                revisaoPlano.replaceChildren(criarResumo(itens));
            }

            const grade = formulario.querySelector("[data-review-schedule]");
            grade.replaceChildren();
            formulario.querySelectorAll("[data-schedule-option]:checked").forEach((opcao) =>
                grade.append(criarResumo([
                    opcao.dataset.dayName,
                    `${opcao.dataset.start} – ${opcao.dataset.end}`,
                    opcao.dataset.className,
                    opcao.dataset.professor
                ])));
        };
        const criarResumo = (linhas) => {
            const bloco = document.createElement("div");
            bloco.className = "bfa-matricula-review__item";
            linhas.filter(Boolean).forEach((linha, indice) => {
                const elemento = indice === 0 ? document.createElement("strong") : document.createElement("span");
                elemento.textContent = linha;
                bloco.append(elemento);
            });
            return bloco;
        };

        const validarPasso = (passo) => {
            limparErro();
            if (passo === 1) {
                if (!modoAluno()) return mostrarErro("Escolha entre aluno existente e novo aluno.", formulario.querySelector("[data-student-mode]"));
                if (modoAluno() === "existente" && !alunoSelecionado())
                    return mostrarErro("Selecione um aluno da unidade.", formulario.querySelector("[data-student-search]"));
                if (modoAluno() === "novo") {
                    const nome = formulario.querySelector("#NovoAluno_NomeCompleto");
                    const nascimento = formulario.querySelector("#NovoAluno_DataNascimentoTexto");
                    if (!nome.value.trim()) return mostrarErro("Informe o nome completo do aluno.", nome);
                    const data = analisarData(nascimento.value);
                    if (!data) return mostrarErro("Informe uma data de nascimento válida.", nascimento);
                    if (data > new Date()) return mostrarErro("A data de nascimento não pode estar no futuro.", nascimento);
                }
            }
            if (passo === 2) {
                const cards = [...formulario.querySelectorAll("[data-guardian-card]")];
                for (const card of cards) {
                    const nome = card.querySelector('[name$=".NomeCompleto"]');
                    const telefone = card.querySelector('[name$=".Telefone"]');
                    const email = card.querySelector('[name$=".Email"]');
                    const relacao = card.querySelector('[name$=".TipoRelacao"]');
                    if (!nome.value.trim()) return mostrarErro("Informe o nome completo do responsável.", nome);
                    if (!telefone.value.trim() && !email.value.trim()) return mostrarErro("Informe telefone ou e-mail do responsável.", telefone);
                    if (!relacao.value) return mostrarErro("Informe o tipo de relação.", relacao);
                    if (relacao.value === "Outro") {
                        const descricao = card.querySelector('[name$=".DescricaoRelacao"]');
                        if (!descricao.value.trim()) return mostrarErro("Descreva a relação.", descricao);
                    }
                }
                const inicio = analisarData(formulario.querySelector("#DataInicioTexto").value) ?? new Date();
                const nascimento = nascimentoSelecionado();
                if (nascimento && idadeEm(nascimento, inicio) < 18
                    && !possuiResponsavelExistente() && cards.length === 0)
                    return mostrarErro("Informe pelo menos um responsável para o aluno menor de idade.", formulario.querySelector("[data-add-guardian]"));
            }
            if (passo === 3) {
                const data = formulario.querySelector("#DataInicioTexto");
                if (!analisarData(data.value)) return mostrarErro("Informe uma data de início válida.", data);
                if (!planoSelecionado()) return mostrarErro("Selecione um plano elegível.", formulario.querySelector("[data-plan-list]"));
                const mensalidade = formulario.querySelector("#ValorMensalContratadoTexto");
                if (!(numeroPtBr(mensalidade.value) > 0)) return mostrarErro("Informe a mensalidade contratada.", mensalidade);
                const taxa = formulario.querySelector("[data-fee-value]");
                if (formulario.querySelector("[data-fee-toggle]").checked && !(numeroPtBr(taxa.value) > 0))
                    return mostrarErro("Informe a taxa ou marque a isenção.", taxa);
            }
            if (passo === 4) {
                const selecionados = formulario.querySelectorAll("[data-schedule-option]:checked");
                const limite = Number(planoSelecionado()?.dataset.planFrequency ?? 0);
                if (!selecionados.length) return mostrarErro("Selecione ao menos um horário para a Grade.", formulario.querySelector("[data-schedule-list]"));
                if (selecionados.length > limite) return mostrarErro("A Grade ultrapassa o limite do plano.");
            }
            return true;
        };

        formulario.addEventListener("change", async (evento) => {
            alterado = true;
            if (evento.target.matches("[data-student-mode]")) atualizarModoAluno();
            if (evento.target.matches("[data-student-option]")) {
                atualizarResponsaveisExistentes();
                atualizarOrientacaoResponsavel();
            }
            if (evento.target.matches("[data-primary-guardian]") && evento.target.checked)
                formulario.querySelectorAll("[data-primary-guardian]").forEach((item) => {
                    if (item !== evento.target) item.checked = false;
                });
            if (evento.target.matches("[data-relation-type]")) atualizarOutro(evento.target);
            if (evento.target.matches("[data-plan-option]")) {
                atualizarCamposPlano(true);
                await carregarHorarios();
            }
            if (evento.target.matches("[data-fee-toggle]")) atualizarTaxa();
            if (evento.target.matches("[data-schedule-option]")) atualizarGrade();
            if (evento.target.id === "DataInicioTexto") {
                atualizarOrientacaoResponsavel();
                await carregarPlanos();
            }
        });
        formulario.addEventListener("input", (evento) => {
            alterado = true;
            if (evento.target.matches("[data-student-search]")) filtrarAlunos();
            if (evento.target.id === "NovoAluno_DataNascimentoTexto") atualizarOrientacaoResponsavel();
        });
        formulario.addEventListener("click", (evento) => {
            const adicionar = evento.target.closest("[data-add-guardian]");
            if (adicionar) adicionarResponsavel();
            const remover = evento.target.closest("[data-remove-guardian]");
            if (remover) {
                remover.closest("[data-guardian-card]").remove();
                reindexarResponsaveis();
                alterado = true;
            }
            const proximo = evento.target.closest("[data-next-step]");
            if (proximo && validarPasso(passoAtual)) irPara(passoAtual + 1);
            if (evento.target.closest("[data-previous-step]")) irPara(passoAtual - 1);
            const editar = evento.target.closest("[data-edit-step]");
            if (editar) irPara(Number(editar.dataset.editStep));
            const indicador = evento.target.closest("[data-go-step]");
            if (indicador && Number(indicador.dataset.goStep) <= maiorPasso)
                irPara(Number(indicador.dataset.goStep));
            const cancelar = evento.target.closest("[data-cancel-wizard]");
            if (cancelar && alterado && !globalThis.confirm("Os dados preenchidos serão descartados."))
                evento.preventDefault();
        });
        formulario.addEventListener("keydown", (evento) => {
            if (evento.key === "Enter" && evento.target.tagName === "INPUT"
                && evento.target.type !== "submit") evento.preventDefault();
        });
        formulario.addEventListener("submit", (evento) => {
            if (enviando) {
                evento.preventDefault();
                return;
            }
            for (let passo = 1; passo <= 4; passo += 1) {
                if (!validarPasso(passo)) {
                    evento.preventDefault();
                    irPara(passo);
                    return;
                }
            }
            enviando = true;
            alterado = false;
            const botao = formulario.querySelector("[data-submit-matricula]");
            botao.disabled = true;
            botao.textContent = botao.dataset.loadingText;
            botao.setAttribute("aria-busy", "true");
        });

        formulario.querySelectorAll("[data-relation-type]").forEach(atualizarOutro);
        atualizarModoAluno();
        atualizarTaxa();
        atualizarCamposPlano(false);
        atualizarGrade();
        irPara(passoAtual);
        if (planoSelecionado()) void carregarHorarios();
    };

    const iniciarGrade = (formulario) => {
        const opcoes = [...formulario.querySelectorAll('input[name="TurmaHorarioIds"]')];
        const dataInput = formulario.querySelector("#DataInicioTexto");
        const dataMinimaTexto = dataInput?.dataset.minDate;
        const dataMinima = dataMinimaTexto ? analisarData(dataMinimaTexto) : null;

        const mostrarErroGrade = (mensagem) => {
            let erro = formulario.querySelector(".bfa-grade-date-error");
            if (!erro) {
                erro = document.createElement("p");
                erro.className = "bfa-validation-message bfa-grade-date-error";
                erro.setAttribute("role", "alert");
                dataInput?.parentElement?.appendChild(erro);
            }
            erro.textContent = mensagem;
            erro.hidden = false;
            dataInput?.focus();
        };
        const limparErroGrade = () => {
            const erro = formulario.querySelector(".bfa-grade-date-error");
            if (erro) erro.hidden = true;
        };

        const validarDataMinima = () => {
            if (!dataInput || !dataMinima) return true;
            limparErroGrade();
            const digitada = analisarData(dataInput.value);
            if (!digitada) return true;
            if (digitada < dataMinima) {
                mostrarErroGrade(
                    `A nova Grade deve começar a partir de ${dataMinimaTexto}. ` +
                    `Horários removidos não podem terminar antes de começar.`);
                return false;
            }
            return true;
        };

        if (dataInput) {
            dataInput.addEventListener("change", () => {
                limparErroGrade();
            });
        }

        formulario.addEventListener("submit", (evento) => {
            if (!validarDataMinima()) {
                evento.preventDefault();
            }
        });

        const atualizar = () => {
            const selecionados = opcoes.filter((opcao) => opcao.checked);
            const limite = Number(formulario.dataset.gradeLimit ?? 0);
            opcoes.forEach((opcao) => {
                const card = opcao.closest(".bfa-matricula-schedule-card");
                const conflito = !opcao.checked && selecionados.some((item) =>
                    item.dataset.day === opcao.dataset.day
                    && item.dataset.start < opcao.dataset.end
                    && opcao.dataset.start < item.dataset.end);
                const excede = !opcao.checked && limite > 0 && selecionados.length >= limite;
                const indisponivel = opcao.dataset.baseUnavailable === "true";
                opcao.disabled = indisponivel || conflito || excede;
                card.classList.toggle("is-selected", opcao.checked);
                card.classList.toggle("is-conflict", conflito);
                card.classList.toggle("is-limit", excede && !conflito);
                const indicacao = card.querySelector("small");
                if (indicacao && !indisponivel)
                    indicacao.textContent = conflito ? "Conflita com a seleção"
                        : excede ? "Limite do plano atingido"
                            : opcao.checked ? "Selecionado" : "Selecionar";
            });
        };
        opcoes.forEach((opcao) => {
            opcao.dataset.baseUnavailable = String(opcao.disabled);
            opcao.addEventListener("change", atualizar);
        });
        atualizar();
    };

    const iniciarFinalizar = (formulario) => {
        const dataInput = formulario.querySelector("#DataFinalTexto");
        const dataMinimaTexto = dataInput?.dataset.minDate;
        const dataMinima = dataMinimaTexto ? analisarData(dataMinimaTexto) : null;

        const mostrarErroFinalizar = (mensagem) => {
            let erro = formulario.querySelector(".bfa-finalizar-date-error");
            if (!erro) {
                erro = document.createElement("p");
                erro.className = "bfa-validation-message bfa-finalizar-date-error";
                erro.setAttribute("role", "alert");
                dataInput?.parentElement?.appendChild(erro);
            }
            erro.textContent = mensagem;
            erro.hidden = false;
            dataInput?.focus();
        };
        const limparErroFinalizar = () => {
            const erro = formulario.querySelector(".bfa-finalizar-date-error");
            if (erro) erro.hidden = true;
        };

        const validarDataMinima = () => {
            if (!dataInput || !dataMinima) return true;
            limparErroFinalizar();
            const digitada = analisarData(dataInput.value);
            if (!digitada) return true;
            if (digitada < dataMinima) {
                mostrarErroFinalizar(
                    `A data final deve ser a partir de ${dataMinimaTexto}. ` +
                    `A grade atual não pode ser encerrada antes do seu início.`);
                return false;
            }
            return true;
        };

        if (dataInput) {
            dataInput.addEventListener("change", () => {
                limparErroFinalizar();
            });
        }

        formulario.addEventListener("submit", (evento) => {
            if (!validarDataMinima()) {
                evento.preventDefault();
            }
        });
    };

    const iniciar = () => {
        document.querySelectorAll("[data-bfa-matricula-wizard]").forEach(iniciarWizard);
        document.querySelectorAll("[data-bfa-matricula-grade]").forEach(iniciarGrade);
        document.querySelectorAll("[data-bfa-matricula-finalizar]").forEach(iniciarFinalizar);
    };
    if (document.readyState === "loading")
        document.addEventListener("DOMContentLoaded", iniciar, { once: true });
    else iniciar();
})();
