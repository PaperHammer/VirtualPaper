# VirtualPaper: privacidade, processamento por IA e direitos autorais

Versão do documento: 1.0. Atualizado em: 2026-11-15. Publicador e mantenedor do projeto: PaperHammer.

Este documento explica como as edições EXE e Microsoft Store do VirtualPaper tratam os dados e quais direitos devem ser considerados ao usar os recursos de IA e o conteúdo incluído. Sites e serviços externos têm suas próprias políticas.

<a id="privacy-policy"></a>
## Política de privacidade

### Arquivos e configurações locais

O VirtualPaper lê as imagens, vídeos, imagens animadas e projetos Web que você escolhe para exibir, editar ou processar. Metadados, miniaturas, favoritos, tags, predefinições de efeitos, caminhos de projetos recentes, configurações e disposições dos monitores são armazenados localmente. Não é necessário ter uma conta VirtualPaper. Esses materiais e registros não são enviados ao mantenedor para inferência de IA, publicidade ou análise de comportamento.

A pasta padrão é `%LOCALAPPDATA%\VirtualPaper`. O Windows pode redirecionar dados de aplicativos empacotados. As bibliotecas podem usar o local escolhido nas Configurações; projetos e imagens exportados ficam no local que você selecionar. Prévia, desfazer, mapas de profundidade e dados do navegador podem criar arquivos locais adicionais. Eles não são um backup na nuvem.

### Diagnóstico e relatos

Os logs locais podem conter versões do aplicativo e do Windows, idioma, informações de CPU/GPU, erros, caminhos de arquivos e detalhes de reprodução. A exportação cria um ZIP local com logs, configurações do usuário e disposições dos papéis de parede. Ele não é enviado automaticamente ao PaperHammer. Revise o arquivo antes de compartilhá-lo: caminhos podem revelar seu nome de usuário do Windows ou nomes de projetos privados. Relatos e anexos publicados voluntariamente no GitHub são tratados pelo GitHub e podem ser públicos.

### Localização

Ao ativar a percepção de horário, o aplicativo solicita acesso à localização do Windows e usa latitude e longitude localmente para calcular o nascer e o pôr do sol. Sem acesso, usa um local padrão; a iluminação pode não corresponder à sua região. Desative a função para interromper suas solicitações de localização. A permissão também pode ser gerenciada em Privacidade e segurança do Windows. O VirtualPaper não envia essas coordenadas ao mantenedor para esse cálculo.

### Rede e serviços de terceiros

A edição EXE pode acessar o GitHub e servidores de download para verificar e obter atualizações do aplicativo ou de plugins. A instalação pode baixar componentes de execução da Microsoft. A distribuição e as atualizações da Store são gerenciadas pela Microsoft. As solicitações expõem ao destinatário informações de conexão, como endereço IP e cabeçalhos. Abrir links de ajuda, comentários ou código-fonte conecta ao site escolhido. Papéis de parede e projetos Web importados podem carregar por conta própria scripts, mídias ou serviços remotos controlados pelos autores.

O diagnóstico do WebView2 e o SmartScreen enviam informações à Microsoft; consulte sua [declaração de privacidade](https://privacy.microsoft.com/privacystatement). Alguns relatórios seguem as configurações de diagnóstico do Windows. Esses mecanismos são separados da inferência local do VirtualPaper.

### Retenção, segurança e escolhas

Configurações, bibliotecas, projetos, caches de profundidade e perfis de navegador salvos permanecem locais até serem removidos por você ou por uma operação de limpeza ou desinstalação aplicável. Resultados temporários de IA ou edição podem ser apagados na próxima inicialização; salve-os ou adicione-os à biblioteca para mantê-los. Nem todos os logs e caches têm um prazo fixo de exclusão automática. Desinstalar não garante a remoção de bibliotecas movidas ou arquivos exportados.

Você pode consultar, exportar, corrigir ou excluir arquivos locais e alterar as configurações correspondentes. Antes de remover dados manualmente, feche o VirtualPaper e seus processos em segundo plano e faça backup dos arquivos necessários. Os dados locais dependem das proteções da conta do Windows e do sistema de arquivos; o VirtualPaper não fornece um cofre criptografado separado. Use projetos confiáveis e proteja sua conta do Windows.

### Contato e alterações

Para dúvidas de privacidade ou solicitações sobre informações fornecidas voluntariamente ao projeto, contate PaperHammer pelo [GitHub Issues](https://github.com/PaperHammer/VirtualPaper/issues). Não publique informações sensíveis; pergunte primeiro como enviá-las de forma adequada. Informações mantidas pelo GitHub, pela Microsoft ou por serviços externos de um papel de parede seguem os controles do respectivo provedor. Este aviso será atualizado se o comportamento relevante mudar; a versão e a data identificam a cópia incluída no aplicativo.

<a id="ai-processing-and-rights"></a>
## Processamento por IA e direitos

A estimativa de profundidade, transferência de estilo e melhoria de imagens usam modelos no seu computador, com CPU/GPU conforme a função e as configurações. Essas inferências não chamam serviços de IA na nuvem. Os arquivos dos modelos precisam estar disponíveis localmente. Qualidade e velocidade dependem da origem, do modelo e do hardware; detalhes gerados podem ser imprecisos e o mapa de profundidade é uma estimativa, não uma medição.

Processar um conteúdo com IA não concede, por si só, direitos autorais sobre o material original ou o resultado, nem permissão para usar materiais de outras pessoas. A proteção do resultado e a titularidade dos direitos dependem da legislação aplicável, da licença do material original e da contribuição criativa humana. As restrições existentes não desaparecem automaticamente após o processamento por IA. Antes de publicar, redistribuir ou usar um resultado comercialmente, confirme que possui os direitos ou as permissões necessários. Direitos autorais, de imagem, de marca e outros direitos de terceiros também devem ser considerados.

O VirtualPaper não reivindica a propriedade das entradas ou dos resultados apenas pelo uso do aplicativo. A licença de código aberto não coloca automaticamente seus materiais ou saídas sob GPL. As licenças dos modelos e componentes de execução são separadas das permissões das imagens; um modelo gratuito não garante saídas sem restrições. O VirtualPaper não garante ausência de violação de direitos, exclusividade ou adequação comercial. Este aviso não substitui as permissões necessárias nem limita direitos legais.

Algumas paisagens incluídas foram criadas com geração de imagens da OpenAI durante a preparação da coleção. Isso é separado da inferência local do aplicativo e não significa que suas imagens sejam enviadas à OpenAI. Os avisos individuais descrevem origem e condições; não se reivindicam direitos autorais exclusivos sobre essas imagens geradas por IA.

<a id="copyright-and-licenses"></a>
## Direitos autorais e licenças

O código do VirtualPaper é distribuído sob a [GNU GPL versão 3](../licenses/GPL-3.0.txt). O código-fonte e as versões estão no [repositório VirtualPaper](https://github.com/PaperHammer/VirtualPaper). As condições de garantia e redistribuição constam na licença; direitos legais obrigatórios permanecem inalterados.

Componentes de terceiros e pesos de modelos estão sujeitos aos respectivos avisos de direitos autorais e licenças. Consulte as [informações sobre terceiros](../THIRD-PARTY.txt), além das licenças e dos arquivos NOTICE originais incluídos. As traduções e explicações são fornecidas como referência; não substituem as condições originais nem concedem uma autorização geral para materiais de terceiros.

Os papéis de parede incluídos têm origem e condições próprias. O vídeo original de Santa Rosa Waves é de Blervis e a filmagem original de Bigora Cascade é de Termininja; as adaptações incluídas usam CC0. Outros avisos identificam as imagens geradas ou o código de animação do VirtualPaper. Preserve o `NOTICE.txt` e as licenças de cada papel de parede ao redistribuí-lo. O crédito de um publicador ou adaptador não substitui o criador original nem altera a licença original.

CC0 não exige atribuição; os registros de criador e fonte documentam a origem sem acrescentar condições de redistribuição.
