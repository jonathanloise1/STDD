import COLORS from './enumColors';
import { TColor } from '../../type/color-type';

export interface IEventStatus {
	[key: string]: { name: string; color: TColor };
}
const EVENT_STATUS: IEventStatus = {
	APPROVED: { name: 'Approved', color: COLORS.SUCCESS.name },
	PENDING: { name: 'Pending', color: COLORS.WARNING.name },
	CANCELED: { name: 'Canceled', color: COLORS.DANGER.name },
	REJECTED: { name: 'Rejected', color: COLORS.DARK.name },
};
export default EVENT_STATUS;
