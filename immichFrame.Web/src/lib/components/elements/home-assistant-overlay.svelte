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

	// Replaces the upstream clock (ShowClock stays off), with its formats.
	const dateLocale = $derived(locale[$configStore.language as keyof typeof locale] ?? locale.enUS);
	const date = $derived(
		format(now, $configStore.clockDateFormat ?? 'eee, MMM d', { locale: dateLocale })
	);
	const time = $derived(format(now, $configStore.clockFormat ?? 'HH:mm'));

	const sensors = $derived(
		(overlay?.sensors ?? []).map((s) => ({
			icon: emoji(s.icon),
			value: s.value ?? '--',
			unit: s.unit
		}))
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
	<div class="ha-notifications">
		<!-- The server keeps each message once: it is a unique key. -->
		{#each notifications as notification (notification.text)}
			<!-- Without a link, taps go through to pause/next/previous underneath. -->
			<button
				class="ha-glass ha-notification"
				class:linked={notification.target !== ''}
				class:busy={notificationBusy}
				onclick={() => openNotification(notification.target)}
			>
				{notification.text}
			</button>
		{/each}
	</div>
{/if}

<button class="ha-glass ha-clock" class:busy={clockBusy} onclick={openHomeAssistant}>
	<span class="ha-time">{time}</span>
	<span class="ha-date">{date}</span>
	{#if sensors.length}
		<span class="ha-sensors">
			{#each sensors as sensor, i (i)}
				<span class="ha-sensor">
					{#if sensor.icon}<span>{sensor.icon}</span>{/if}
					<span class="ha-value">{sensor.value}</span><span class="ha-unit">{sensor.unit}</span>
				</span>
			{/each}
		</span>
	{/if}
</button>

<style>
	/*
	 * Sized on the shorter side of the screen rather than BaseFontSize: small enough to
	 * leave the photo in front, whatever the frame's orientation.
	 */
	.ha-glass,
	:global(#imageinfo.immichframe_image_metadata) {
		color: var(--primary-color);
		background: color-mix(in srgb, var(--secondary-color) 18%, transparent);
		backdrop-filter: blur(1.6vmin) saturate(140%);
		-webkit-backdrop-filter: blur(1.6vmin) saturate(140%);
		border: 1px solid rgb(255 255 255 / 0.14);
		border-radius: 2.2vmin;
		box-shadow: 0 0.6vmin 2.4vmin rgb(0 0 0 / 0.2);
		text-shadow: 0 0.1vmin 0.6vmin rgb(0 0 0 / 0.35);
		user-select: none;
		transition: opacity 0.2s;
	}

	.busy {
		opacity: 0.5;
	}

	/* Above the pause/next/previous grid (z-100): the tap opens Home Assistant. */
	.ha-clock {
		position: fixed;
		left: 3vmin;
		bottom: 3vmin;
		z-index: 110;
		display: flex;
		flex-direction: column;
		align-items: flex-start;
		padding: 1.8vmin 2.6vmin 2vmin;
		cursor: pointer;
		text-align: left;
	}

	.ha-time {
		font-size: 8.5vmin;
		font-weight: 250;
		line-height: 0.95;
		letter-spacing: -0.02em;
		font-variant-numeric: tabular-nums;
	}

	.ha-date {
		margin-top: 0.8vmin;
		font-size: 1.7vmin;
		font-weight: 600;
		letter-spacing: 0.16em;
		text-transform: uppercase;
		opacity: 0.85;
	}

	.ha-sensors {
		align-self: stretch;
		display: flex;
		gap: 2.4vmin;
		margin-top: 1.4vmin;
		padding-top: 1.3vmin;
		border-top: 1px solid rgb(255 255 255 / 0.18);
		font-size: 2.2vmin;
	}

	.ha-sensor {
		display: flex;
		align-items: baseline;
		gap: 0.6vmin;
		white-space: nowrap;
	}

	.ha-value {
		font-weight: 500;
		font-variant-numeric: tabular-nums;
	}

	.ha-unit {
		margin-left: -0.4vmin;
		font-size: 0.75em;
		opacity: 0.75;
	}

	.ha-notifications {
		position: fixed;
		top: 3vmin;
		left: 3vmin;
		right: 3vmin;
		z-index: 110;
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 1.2vmin;
		pointer-events: none;
	}

	.ha-notification {
		max-width: 70vw;
		padding: 1.2vmin 2.6vmin;
		font-size: 2.4vmin;
		font-weight: 500;
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
		opacity: 0.7;
	}

	/* The photo details of asset-info.svelte, restyled from here to leave that file as upstream. */
	:global(#imageinfo.immichframe_image_metadata) {
		bottom: 3vmin;
		right: 3vmin;
		display: flex;
		flex-direction: column-reverse;
		align-items: flex-end;
		gap: 0.3vmin;
		padding: 1.2vmin 2vmin;
	}

	:global(#imageinfo .info-item) {
		margin: 0;
		gap: 0.8vmin;
		font-size: 1.7vmin;
	}

	:global(#imageinfo #imagelocation) {
		font-size: 2.1vmin;
		font-weight: 500;
	}

	:global(#imageinfo #photodate) {
		opacity: 0.8;
	}

	:global(#imageinfo svg) {
		opacity: 0.7;
	}
</style>
