<script lang="ts">
	import { onMount } from 'svelte';
	import { defaults, getBaseUrl } from '$lib/index';
	import { emoji, notificationTarget } from './home-assistant-overlay';

	interface Sensor {
		icon: string;
		value: string | null;
		unit: string;
	}

	interface Overlay {
		connected: boolean;
		sensors: Sensor[];
		/** Newest first. */
		notifications: { message: string; link: string; until: number | null }[];
		memoriesEnabled: boolean;
	}

	interface Props {
		/** Memories were shown or hidden: assets already queued are stale. */
		onMemoriesChanged?: () => void;
	}

	let { onMemoriesChanged }: Props = $props();

	// Home Assistant pushes to the server; this poll only reads the server's copy,
	// so it stays cheap.
	const POLL_MS = 2000;
	const BUSY_MS = 2000;

	let now = $state(new Date());
	let overlay = $state<Overlay | null>(null);
	let clockBusy = $state(false);
	let notificationBusy = $state(false);

	const time = $derived(
		String(now.getHours()).padStart(2, '0') + ':' + String(now.getMinutes()).padStart(2, '0')
	);

	const sensors = $derived(
		(overlay?.sensors ?? [])
			.map((s) => (s.icon ? emoji(s.icon) + ' ' : '') + (s.value ?? '--') + s.unit)
			.join('   ')
	);

	const notifications = $derived(
		(overlay?.notifications ?? [])
			.filter((n) => n.until === null || now.getTime() < n.until)
			.map((n) => ({ text: emoji(n.message), target: notificationTarget(n.link) }))
	);

	async function refresh() {
		try {
			const response = await fetch(getBaseUrl() + 'api/Overlay', {
				headers: defaults.headers as Record<string, string>,
				cache: 'no-store'
			});
			if (!response.ok) return;
			const next: Overlay = await response.json();
			if (overlay && overlay.memoriesEnabled !== next.memoriesEnabled) {
				onMemoriesChanged?.();
			}
			overlay = next;
		} catch {
			// Keep the last values: a missed poll is not worth a blank overlay.
		}
	}

	// An empty invitation: the Home Assistant app finds no server, closes that screen
	// and its task comes back to the foreground as it was. ⚠️ Not #url= (a real
	// invitation), nor navigate (reloads a view), nor https:// (opened in the browser).
	function openHomeAssistant() {
		if (clockBusy) return;
		clockBusy = true;
		setTimeout(() => (clockBusy = false), BUSY_MS);
		window.location.href = 'homeassistant://invite';
	}

	function openNotification(target: string) {
		if (!target || notificationBusy) return;
		notificationBusy = true;
		setTimeout(() => (notificationBusy = false), BUSY_MS);
		window.location.href = target;
	}

	onMount(() => {
		refresh();
		// Every second: a clock showing minutes must still flip on the minute.
		const clock = window.setInterval(() => (now = new Date()), 1000);
		const poll = window.setInterval(refresh, POLL_MS);
		return () => {
			window.clearInterval(clock);
			window.clearInterval(poll);
		};
	});
</script>

{#if notifications.length}
	<div id="ha-notifications">
		<!-- The server keeps each message once: it is a unique key. -->
		{#each notifications as notification (notification.text)}
			<!-- Without a link, taps go through to pause/next/previous underneath. -->
			<button
				class="ha-notification text-frame-primary"
				class:linked={notification.target !== ''}
				class:busy={notificationBusy}
				onclick={() => openNotification(notification.target)}
			>
				{notification.text}
			</button>
		{/each}
	</div>
{/if}

<button
	id="ha-overlay"
	class="text-frame-primary"
	class:busy={clockBusy}
	onclick={openHomeAssistant}
>
	<span id="ha-clock">{time}</span>
	{#if sensors}
		<span id="ha-sensors">{sensors}</span>
	{/if}
</button>

<style>
	button {
		all: unset;
		font-family: sans-serif;
		text-shadow: 0 1px 4px rgba(0, 0, 0, 0.8);
		user-select: none;
		transition: opacity 0.2s;
	}

	button.busy {
		opacity: 0.5;
	}

	#ha-overlay,
	#ha-notifications {
		position: fixed;
		/* Above the pause/next/previous grid (z-100). */
		z-index: 110;
	}

	#ha-overlay {
		left: 24px;
		bottom: 24px;
		display: flex;
		flex-direction: column;
		cursor: pointer;
	}

	#ha-clock {
		font-size: 2.4rem;
		font-weight: 600;
		line-height: 1.1;
	}

	#ha-sensors {
		font-size: 1.1rem;
		margin-top: 4px;
		opacity: 0.9;
		white-space: pre;
	}

	#ha-notifications {
		top: 24px;
		left: 24px;
		right: 24px;
		display: flex;
		flex-direction: column;
		/* Centred without translate(-50%): it would cap the width at half the screen. */
		align-items: center;
		gap: 12px;
		pointer-events: none;
	}

	.ha-notification {
		max-width: 100%;
		box-sizing: border-box;
		padding: 14px 30px;
		border-radius: 20px;
		background: rgba(0, 0, 0, 0.55);
		font-size: 2rem;
		line-height: 1.3;
		text-align: center;
		white-space: pre-line;
		overflow-wrap: anywhere;
		pointer-events: none;
	}

	.ha-notification.linked {
		pointer-events: auto;
		cursor: pointer;
	}

	.ha-notification.linked::after {
		content: ' ›';
	}
</style>
