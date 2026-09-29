/**
 * A pictograph without its U+FE0F selector (⚠, ❤, ☀, 🛋...) renders in black and white:
 * add it. Not before a selector or a skin tone, which would split from it, nor
 * below U+2300 (copyright, trademark, arrows: text, not emoji).
 */
export function emoji(text: string): string {
	return text.replace(/\p{Extended_Pictographic}(?!\uFE0E|\uFE0F|\p{Emoji_Modifier})/gu, (c) =>
		(c.codePointAt(0) ?? 0) < 0x2300 || /\p{Emoji_Presentation}/u.test(c) ? c : c + '\uFE0F'
	);
}

/**
 * "/path" opens that view in the Home Assistant app. Any scheme other than web or
 * homeassistant:// is dropped: no javascript: coming from an automation's text.
 */
export function notificationTarget(link: string | null | undefined): string {
	const trimmed = typeof link === 'string' ? link.trim() : '';
	if (trimmed.startsWith('/')) return 'homeassistant://navigate' + trimmed;
	return /^(https?|homeassistant):\/\//i.test(trimmed) ? trimmed : '';
}
