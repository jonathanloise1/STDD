import type { TFunction } from 'i18next';

/** Stored types of the entities, in the order of the choices of the "Type" field. */
export const AUDIT_ENTITY_TYPES = ['TaskItem', 'OrganizationUser', 'Organization', 'User'] as const;

/** Fields the user never sees on a screen: identifiers, time stamps and bookkeeping. */
const HIDDEN_FIELDS = new Set([
	'id',
	'createdat',
	'updatedat',
	'createdby',
	'updatedby',
	'isdeleted',
	'rowversion',
	'users',
]);

const lowerFirst = (value: string) => value.charAt(0).toLowerCase() + value.slice(1);

/** A field is shown only when the user can see it on a screen. */
export const isVisibleField = (field: string): boolean => {
	const name = lowerFirst(field);
	return !HIDDEN_FIELDS.has(name.toLowerCase()) && !/Id$/.test(name);
};

/** The names of the fields that changed in an update, as stored (JSON array or comma separated). */
export const parseChangedFields = (raw: string | null | undefined): string[] => {
	if (!raw) return [];
	let fields: string[];
	try {
		const parsed = JSON.parse(raw);
		fields = Array.isArray(parsed) ? parsed.map(String) : [];
	} catch {
		fields = raw.split(',').map((field) => field.trim());
	}
	return fields.filter(isVisibleField);
};

/** The values of a change as stored, without the fields the user never sees. */
export const parseValues = (raw: string | null | undefined): [string, unknown][] => {
	if (!raw) return [];
	try {
		const parsed = JSON.parse(raw);
		if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
			return Object.entries(parsed as Record<string, unknown>).filter(([field]) =>
				isVisibleField(field),
			);
		}
	} catch {
		// Not JSON: nothing to list
	}
	return [];
};

const humanize = (field: string): string =>
	lowerFirst(field)
		.replace(/([A-Z])/g, ' $1')
		.replace(/^./, (c) => c.toUpperCase());

/** The label of a field as the screen where it is edited shows it. */
export const fieldLabel = (t: TFunction, entityType: string, field: string): string => {
	const name = lowerFirst(field);
	return t(`fields.${entityType}.${name}`, {
		defaultValue: t(`fields.${name}`, { defaultValue: humanize(field) }),
	});
};

const STATUS_FIELDS_BY_ENTITY: Record<string, string> = {
	TaskItem: 'tasks:status',
};

/** A value as the screen shows it; an empty value is "-". */
export const formatValue = (
	t: TFunction,
	entityType: string,
	field: string,
	value: unknown,
	locale: string,
): string => {
	if (value === null || value === undefined || value === '') return '-';
	const name = lowerFirst(field);
	if (typeof value === 'boolean') return t(value ? 'values.yes' : 'values.no');
	if (name === 'status') {
		const key = STATUS_FIELDS_BY_ENTITY[entityType]
			? `${STATUS_FIELDS_BY_ENTITY[entityType]}${String(value)}`
			: `values.${String(value)}`;
		return t(key, { defaultValue: String(value) });
	}
	if (name === 'role') return t(`values.${String(value)}`, { defaultValue: String(value) });
	if (name === 'preferredLanguage')
		return t(`values.${String(value)}`, { defaultValue: String(value) });
	if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value)) {
		return new Date(value).toLocaleString(locale);
	}
	return String(value);
};
