---
title: project-context
description: This project's specifics. gamedev-team agents and orchestration skills read this at runtime instead of hardcoding project details. Fill it in once per project.
---

# Project Context

> gamedev-team 에이전트와 orchestration 스킬이 런타임에 이 파일을 읽어
> 게임명·톤·경로 등 프로젝트 특정 사항을 파악한다. 프로젝트당 1회 작성한다.
> 값이 비면 해당 기능은 범용 기본값으로 동작한다.

## game
- **name**: The Cheese Thief and the Cat (치즈 도둑과 고양이)
- **genre**: 3D 3인칭 비대칭 멀티 경제전 — 원작 '약올리기 DX' 규칙의 3D 번안 (고양이 1~2 vs 쥐 3~6, 쥐는 개인 경제·고양이 처치는 공동 목표, 시간제한 없음)
- **tone**: 귀엽고 코믹한 스타일라이즈드 — 위험한 돈줄에서 벌고 빠지기(약올리기) × 무장해서 보스 고양이 말려 죽이기(소모전)
- **references**: [약올리기 DX (SC UMS) — 규칙 전체, MECCHA CHAMELEON — 3인칭·자유 카메라·관전 연출만]

## engine
- **stack**: Unity URP (3D) / Netcode for GameObjects 2.12 (호스트 권위) / 개발 UGS Relay+Lobby 예정 → 출시 Steam 전환 · PC 우선

## repos
> commit-push 스킬이 순회하며 커밋·푸시할 레포 목록 (절대경로 또는 프로젝트 상대)
- C:/GitFork/The-Cheese-Thief-and-the-Cat

## paths
> 에이전트·스킬이 참조하는 폴더/문서 규약 (프로젝트 루트 기준 상대경로)
- **gdd**: docs/superpowers/specs/2026-07-06-cheese-thief-and-cat-design.md
- **design**: docs/superpowers/specs/
- **core_loop**: docs/superpowers/specs/2026-07-06-cheese-thief-and-cat-design.md (§5 코어 루프)
- **resources**: <리소스 인벤토리 문서 (없으면 비움)>
- **usage_report**: <리소스 사용 리포트 (없으면 비움)>
- **asset_backlog**: <외부 자산 백로그 폴더 (없으면 비움)>
- **source**: Assets/Scripts/
- **csv**: Assets/Resources/CSVInfo/ (2026-07-31 이식 — 밸런스 수치 원본. `Tools/CTC/Convert CSV → Info`로 스키마 재생성. 맵 후보풀만 여전히 ScriptableObject)
- **gameresource**: <CTC는 GameResource 폴더가 없다 — 에셋은 Assets/Prefabs·Materials·Audio 아래에 직접 둔다>
- **vault**: docs/design-vault/ (기획·구조 문서. 코드를 고치면 「코드 다이어그램.md」의 머메이드를 같이 갱신한다)
- **wesqa**: tools/wesqa/

## mcp
- **unity**: true

## team
> 활성 에이전트. 전부 안 써도 됨.
- [director, client, designer, qa, sound, story, level-design]

## integrations
> 웹 자동화 대상 (Playwright). 안 쓰면 비움.
- **gemini_image**: <이미지 생성 URL (안 쓰면 비움)>
- **sound_gen**: [<음원 생성 URL들 (안 쓰면 비움)>]

## secrets
> secret은 여기 두지 말 것. .claude/settings.local.json 의 env 에 둔다.
> - DISCORD_WEBHOOK_URL : Discord 미러링용 webhook (안 쓰면 미설정 → no-op)
