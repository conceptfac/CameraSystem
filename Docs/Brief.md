# ConceptFactory.Cameras — Brief

## Escopo

Sistema de câmeras diversas (player em 1ª/3ª pessoa, top-down, câmera fixa estilo Resident Evil 1, veículo, cinemática) — reutilizável por QUALQUER sistema de gameplay (Movement, Vehicles, Melee/Aim, cutscenes), nunca o contrário. Conhece só `ConceptFactory.Core`. Nunca referencia `ConceptFactory.Movement` nem sabe que ele existe — quem faz a ponte entre os dois é uma peça externa a ambos (ver seção "Ponte com Movement" abaixo).

## Decisão (2026-09): canal por instância, nunca global

Primeira ideia (rejeitada): um canal único, tipo asset `ScriptableObject` compartilhado por todo o jogo (`CameraChannel.Activate(key)` estático/singleton). Quebra com múltiplos personagens simultâneos (split-screen, vários players locais) — um canal global faria a troca de câmera do Player 2 também disparar as câmeras do Player 1, porque os dois estariam ouvindo o mesmo canal.

**Fix**: o canal vira um componente por instância, `CameraModeController` — um por câmera-rig de cada personagem/player, nunca global. Cada player tem o seu próprio, com seu próprio `event Action<CameraModeKey> ModeChanged`. Os adapters de câmera (vcam do Cinemachine, câmera única sem Cinemachine) se inscrevem só na instância do PRÓPRIO rig (referência local dentro da hierarquia daquele player, nunca busca de cena).

`CameraModeKey` continua um `ScriptableObject`, mas só como IDENTIDADE/rótulo ("ThirdPerson", "FirstPerson", "TopDown") — sem estado nenhum, então pode (e deve) ser compartilhado entre players: os dois podem estar em "ThirdPerson" ao mesmo tempo referenciando o MESMO asset `ThirdPerson.asset`, cada um com seu próprio `CameraModeController` — a troca de um nunca vaza pro outro porque o CANAL é que é por instância, não a chave.

## Implementado

- `Runtime/Scripts/CameraModeKey.cs` — `ScriptableObject`, só identidade.
- `Runtime/Scripts/ICameraModeSource.cs` — interface (`ModeChanged`/`TargetChanged`), o contrato que os adapters de câmera realmente precisam.
- `Runtime/Scripts/CameraModeController.cs` — `MonoBehaviour`, implementação padrão de `ICameraModeSource`, `Activate(CameraModeKey key)`/`Follow(Transform target)`.

## Decisão (2026-09): Cinemachine como dependência opcional de verdade

Nem todo GD vai usar Cinemachine — o pacote não pode exigir isso pra funcionar (o `CameraModeController`/`CameraModeKey` acima já não sabem que Cinemachine existe). Mas quem usa Cinemachine merece adapters prontos, não só um contrato genérico.

**Fix**: `Runtime/Cinemachine/` ganhou seu PRÓPRIO assembly definition (`ConceptFactory.Cameras.Cinemachine.asmdef`), separado do `ConceptFactory.Cameras.Runtime` principal, referenciando `Unity.Cinemachine` (GUID `4307f53044263cf4b835bd812fc161a4`, pacote `com.unity.cinemachine`).

**Correção (2026-09)**: a primeira versão dessa doc afirmava que "se o pacote não estiver instalado, o Unity pula a compilação da assembly inteira, sem erro" — isso está ERRADO, confirmado depois via busca (o usuário cobrou explicação). Comportamento real do Unity: uma referência de asmdef pra uma assembly que não existe gera só um AVISO no Console — a assembly continua tentando compilar normalmente. Isso significa que qualquer código usando tipos do Cinemachine sem proteção nenhuma (como `CinemachineModeCamera.cs` tinha) dá ERRO DE COMPILAÇÃO de verdade se o pacote não estiver instalado, não silêncio nenhum.

**Fix de verdade, três peças juntas** (o usuário pegou que faltava a segunda): `versionDefines` sozinho só CRIA o símbolo, não faz nada com ele — precisa de `defineConstraints` pra realmente usar esse símbolo e excluir a assembly inteira da compilação:
```json
"defineConstraints": ["CONCEPTFACTORY_CINEMACHINE_INSTALLED"],
"versionDefines": [{ "name": "com.unity.cinemachine", "expression": "", "define": "CONCEPTFACTORY_CINEMACHINE_INSTALLED" }]
```
Com isso, sem o pacote instalado, o Unity nem tenta compilar essa assembly — não resolve a referência a `Unity.Cinemachine`, não gera aviso nenhum, exclui tudo antes de começar. O `#if CONCEPTFACTORY_CINEMACHINE_INSTALLED` envolvendo o conteúdo INTEIRO de `CinemachineModeCamera.cs` continua no lugar como segunda camada de defesa (redundante quando o `defineConstraints` já exclui a assembly, mas documenta a intenção direto no arquivo pra quem ler só ele). `expression: ""` = define o símbolo em QUALQUER versão instalada do pacote, só checando presença.

`com.unity.cinemachine` NÃO entra em `dependencies` no `package.json` — isso forçaria o Package Manager a instalar Cinemachine em todo projeto que importar `ConceptFactory.Cameras`, quebrando a opcionalidade. Só o asmdef sabe da referência, e só se o pacote já estiver lá por conta própria.

**`GTACamera.prefab` movido de `com.conceptfactory.movement/Runtime/Prefabs/` pra cá** (`Runtime/Cinemachine/Prefabs/GTACamera.prefab`, mesmo GUID preservado) — é uma câmera orbital Cinemachine pura (CinemachineCamera + OrbitalFollow + RotationComposer + Deoccluder + InputAxisController), sem nenhuma referência a tipos do Movement; não tinha motivo pra morar num pacote que se define como agnóstico de câmera. Vira o primeiro exemplo real dentro do `Runtime/Cinemachine/` — futuro adapter (`CinemachineModeCamera` ou similar, ligando `CameraModeController`/`CameraModeKey` à prioridade da vcam) mora nessa mesma pasta.

## Implementado (2026-09): `CinemachineModeCamera`

`Runtime/Cinemachine/Scripts/CinemachineModeCamera.cs` — vai em cada vcam (`[RequireComponent(typeof(CinemachineVirtualCameraBase))]`, a classe-base real de `CinemachineCamera`, então funciona com qualquer tipo de vcam do CM3, não só a orbital). Assina `CameraModeController.ModeChanged` e ajusta a própria `Priority` (`activePriority` se a key bater, `inactivePriority` senão) — o Brain do Cinemachine é quem faz o blend de verdade entre quem tem a prioridade mais alta, esse componente só mexe nesse número; os dois NÃO são redundantes, resolvem problemas diferentes (Brain: mecânica de blend, cego a "modos"; este componente: qual vcam deveria estar ganhando agora, traduzindo o evento semântico em número cru). Confirmado no código-fonte do pacote instalado (3.1.7): `CinemachineVirtualCameraBase.Priority` é campo público do tipo `PrioritySettings`, que tem conversão implícita de `int` (`vcam.Priority = 10;` já funciona direto, sem precisar montar o struct na mão).

**Correção (2026-09)**: a primeira versão tinha um campo `[SerializeField] CameraModeController controller` pra arrastar na mão em CADA vcam — o usuário apontou que isso é fricção redundante desnecessária. Fix: `controller` agora é resolvido sozinho via `GetComponentInParent<...>()` no `Awake()` — **a vcam precisa ser filha (direta ou indireta) do objeto que anuncia troca de modo/alvo** (ex.: o próprio "Main Camera" que já tem o `CinemachineBrain` pode ser o pai de todas as vcams do rig). Sem campo nenhum pra esquecer de preencher, e qualquer vcam nova adicionada sob esse rig já sai funcionando.

**Correção (2026-09): `ICameraModeSource` extraído, `CameraModeController` deixou de ser obrigatório**: o usuário achou estranho precisar de DOIS componentes no rig (`CameraModeController` + a peça-ponte com Movement, `CharacterViewSwitcher`) quando só a segunda de fato aciona alguma coisa. Extraímos a interface `ICameraModeSource` (`Runtime/Scripts/ICameraModeSource.cs` — `event Action<CameraModeKey> ModeChanged` + `event Action<Transform> TargetChanged`), que `CameraModeController` implementa. `CinemachineModeCamera` agora faz `GetComponentInParent<ICameraModeSource>()` (a interface), não a classe concreta — então QUALQUER coisa que já implemente essa interface serve, incluindo uma peça de fora do pacote (`CharacterViewSwitcher`, que já precisa conhecer Movement E Cameras, então pode simplesmente SER a fonte também, sem precisar de um `CameraModeController` adicional ao lado). `CameraModeController` continua existindo — é o default plug-and-play pra quem usa `Cameras` sem nenhum sistema tipo Movement junto.

**Assets shipados**: `Runtime/Cinemachine/Configs/ThirdPerson.asset` e `TopDown.asset` — `CameraModeKey`s prontos pros dois estilos que já são 100% suportados do lado do Movement hoje (ambos reusam o mesmo `ThirdPersonMode`).

**`TopDownCamera.prefab` (2026-09)**: segunda vcam, mesmo formato do `GTACamera.prefab` (mesmos scripts do Cinemachine 3.1.7, só valores diferentes), já com `CinemachineModeCamera` (`key` = `TopDown.asset`) embutido — igual o `GTACamera.prefab` já ganhou o dele (`key` = `ThirdPerson.asset`) direto no asset, sem precisar configurar nada na instância além de ser filha do rig. Diferenças de configuração:
- `CinemachineOrbitalFollow.VerticalAxis` travado perto do topo (`Value`/`Center`: 85, `Range`: {70, 90}, recentralização rápida) — força a câmera quase diretamente acima do alvo. `HorizontalAxis.Range` travado em `{0, 0}` — sem órbita horizontal, ângulo fixo (estilo mapa, não gira com o jogador).
- Orbit "Top" com `Height: 15`/`Radius: 1` — é essa faixa do perfil que domina quando `VerticalAxis` fica perto de 90.
- Sem `CinemachineDeoccluder` (câmera alta o suficiente que oclusão por geometria do chão não é uma preocupação real) nem `CinemachineInputAxisController` (view travada, sem controle de órbita do jogador) — diferente do `GTACamera`, que tem os dois.
- Mesmo aviso de sempre: valores de posição/altura/FOV são um primeiro palpite, não testados visualmente ainda — espera ajuste depois do primeiro Play.

## Pendente

- Câmera única sem Cinemachine, câmera fixa estilo Resident Evil 1 — nenhum adapter implementado ainda.
- Câmera fixa estilo RE1 provavelmente não reage a `CameraModeKey` (que é sobre ESTILO de movimentação/câmera), e sim a posição/área do personagem — pode precisar de um conceito próprio (zonas de câmera fixa), não necessariamente o mesmo `CameraModeController`. Não decidido ainda.

## Ponte com Movement — o sinal parte de quem é possuído, `CameraModeController` continua 100% puro (2026-09)

Passou por três desenhos até fechar:
1. `CharacterViewSwitcher` (Sample separado, morava no rig ao lado de `CameraModeController`) — usuário achou estranho precisar de DOIS componentes no rig.
2. `LocomotionCameraModeController : CameraModeController` (subclasse, dependência OPCIONAL de Movement dentro do próprio `Cameras`, em `Runtime/Movement/`) — resolvia o "dois componentes", mas ainda exigia o `Cameras` saber que `Movement` pode existir (mesmo que opcionalmente).
3. **Fechado**: o usuário virou a direção — é o objeto POSSUÍDO (personagem, veículo) que já sabe de tudo (é ele que decide "eu aceito essas views"), então é ELE quem manda o sinal pro `CameraModeController`, que continua exatamente como nasceu: sem nenhuma noção de Movement, opcional ou não, nenhuma assembly nova em `Cameras`.

`CameraModeController` não mudou nada nessa versão final — continua só `Activate(key)`/`Follow(target)`, público, chamável por QUALQUER coisa que tenha uma referência a ele. A peça nova (`PossessionViewController`) mora inteira do lado de `ConceptFactory.Movement` (`Samples/ViewSwitching/`), com sua própria assembly separada da principal — se `ConceptFactory.Cameras` não estiver instalado, essa assembly inteira é pulada pelo Unity (mesmo comportamento de referência-não-resolvida já usado pro Cinemachine), e o resto do Movement nem percebe.

**Setup final**: no rig, só o `CameraModeController` puro (nenhuma subclasse, nenhuma extensão). No personagem/veículo, `PossessionViewController` (ao lado do `LocomotionModeProvider`) — guarda a própria lista de views e, quando alguém chama `Bind(cameraModeController)` nele (o spawner, ao instanciar), passa a mandar os sinais direto.

Observação de configuração: com Cinemachine, o `referenceCamera` passado pro `LocomotionModeProvider.SwitchTo` é sempre a MESMA câmera física (a que tem o `CinemachineBrain`) em toda troca de view — só a vcam com prioridade mais alta muda (via `CinemachineModeCamera`), a câmera renderizando de verdade nunca muda de objeto.
