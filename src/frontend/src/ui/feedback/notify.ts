import type { ReactNode } from 'react';
import { toast } from 'react-toastify';

/** Short messages after an action (frontend.md 10.1). They close by themselves after 5 seconds. */
export const notify = {
	success: (text: ReactNode) => toast.success(text),
	info: (text: ReactNode) => toast.info(text),
	warning: (text: ReactNode) => toast.warning(text),
	error: (text: ReactNode) => toast.error(text),
};
