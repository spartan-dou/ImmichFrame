<script lang="ts">
	import { onMount } from 'svelte';
	import { format } from 'date-fns';
	import * as locale from 'date-fns/locale';
	import { defaults, getBaseUrl } from '$lib/index';
	import { configStore } from '$lib/stores/config.store';
	import { emoji, notificationTarget } from './home-assistant-overlay';

	interface Sensor {
		icon: string;
		value: string | null;
		unit: string;
	}

	interface Overlay {
		sensors: Sensor[];
		/** Newest first. */
		notifications: { message: string; link: string; until: number | null }[];
		memoriesEnabled: boolean;
		memoriesOnly: boolean;
	}

	interface Props {
		/** A memories switch changed: assets already queued are stale. */
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

	// Same formats and look as the upstream clock, which this replaces: ShowClock stays off.
	const dateLocale = $derived(locale[$configStore.language as keyof typeof locale] ?? locale.enUS);
	const date = $derived(
		format(now, $configStore.clockDateFormat ?? 'eee, MMM d', { locale: dateLocale })
	);
	const time = $derived(format(now, $configStore.clockFormat ?? 'HH:mm'));

	// The upstream Style setting, as the clock and the photo details apply it.
	const clockStyle = $derived(
		{
			solid: 'bg-frame-secondary rounded-tr-2xl',
			transition: 'bg-linear-to-r from-frame-secondary from-0% pr-10',
			blur: 'backdrop-blur-lg rounded-tr-2xl'
		}[$configStore.style ?? ''] ?? ''
	);
	// Unlike the clock, a message needs a backdrop to stay readable over any photo.
	const notificationStyle = $derived(
		{ solid: 'bg-frame-secondary', blur: 'backdrop-blur-lg' }[$configStore.style ?? ''] ??
			'bg-frame-secondary/50'
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
			if (
				overlay &&
				(overlay.memoriesEnabled !== next.memoriesEnabled ||
					overlay.memoriesOnly !== next.memoriesOnly)
			) {
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
	<div class="pointer-events-none fixed inset-x-0 top-0 z-110 flex flex-col items-center gap-3 p-3">
		<!-- The server keeps each message once: it is a unique key. -->
		{#each notifications as notification (notification.text)}
			<!-- Without a link, taps go through to pause/next/previous underneath. -->
			<button
				class="ha-notification max-w-full rounded-2xl px-6 py-3 text-center text-xl font-semibold whitespace-pre-line text-frame-primary text-shadow-sm transition-opacity sm:text-xl md:text-2xl lg:text-3xl {notificationStyle}"
				class:linked={notification.target !== ''}
				class:opacity-50={notificationBusy}
				onclick={() => openNotification(notification.target)}
			>
				{notification.text}
			</button>
		{/each}
	</div>
{/if}

<!-- Above the pause/next/previous grid (z-100), unlike the upstream clock: the tap opens Home Assistant. -->
<button
	id="ha-clock"
	class="fixed bottom-0 left-0 z-110 cursor-pointer p-3 text-center text-frame-primary drop-shadow-2xl transition-opacity select-none {clockStyle}"
	class:opacity-50={clockBusy}
	onclick={openHomeAssistant}
>
	<p class="mt-2 text-sm font-thin text-shadow-sm sm:text-sm md:text-base lg:text-xl">{date}</p>
	<p class="mt-2 text-4xl font-bold text-shadow-lg sm:text-4xl md:text-6xl lg:text-8xl">{time}</p>
	{#if sensors}
		<p
			class="text-xl font-semibold whitespace-pre text-shadow-sm sm:text-xl md:text-2xl lg:text-3xl"
		>
			{sensors}
		</p>
	{/if}
</button>

<style>
	.ha-notification {
		overflow-wrap: anywhere;
		pointer-events: none;
		user-select: none;
	}

	.ha-notification.linked {
		pointer-events: auto;
		cursor: pointer;
	}

	.ha-notification.linked::after {
		content: ' ›';
	}
</style>
