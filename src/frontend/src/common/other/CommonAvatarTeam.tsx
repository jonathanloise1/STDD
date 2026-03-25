import React, { FC, memo, ReactNode } from 'react';
import Avatar, { AvatarGroup } from '../../components/Avatar';
import USERS from '../data/userDummyData';

interface ICommonAvatarTeamProps {
	children: ReactNode;
	isAlignmentEnd?: boolean;
}
const CommonAvatarTeam: FC<ICommonAvatarTeamProps> = ({ children, isAlignmentEnd }) => {
	return (
		<>
			{children && !isAlignmentEnd && <span className='me-3'>{children}</span>}
			<AvatarGroup>
				<Avatar
					src={USERS.GRACE.src}
					srcSet={USERS.GRACE.srcSet}
					// @ts-ignore
					color={USERS.GRACE.color}
					userName={`${USERS.GRACE.name} ${USERS.GRACE.surname}`}
				/>
				<Avatar
					src={USERS.JANE.src}
					srcSet={USERS.JANE.srcSet}
					// @ts-ignore
					color={USERS.JANE.color}
					userName={`${USERS.JANE.name} ${USERS.JANE.surname}`}
				/>
				<Avatar
					src={USERS.RYAN.src}
					srcSet={USERS.RYAN.srcSet}
					// @ts-ignore
					color={USERS.RYAN.color}
					userName={`${USERS.RYAN.name} ${USERS.RYAN.surname}`}
				/>
				<Avatar
					src={USERS.ELLA.src}
					srcSet={USERS.ELLA.srcSet}
					// @ts-ignore
					color={USERS.ELLA.color}
					userName={`${USERS.ELLA.name} ${USERS.ELLA.surname}`}
				/>
				<Avatar
					src={USERS.CHLOE.src}
					srcSet={USERS.CHLOE.srcSet}
					// @ts-ignore
					color={USERS.CHLOE.color}
					userName={`${USERS.CHLOE.name} ${USERS.CHLOE.surname}`}
				/>
				<Avatar
					src={USERS.SAM.src}
					srcSet={USERS.SAM.srcSet}
					// @ts-ignore
					color={USERS.SAM.color}
					userName={`${USERS.SAM.name} ${USERS.SAM.surname}`}
				/>
			</AvatarGroup>
			{children && isAlignmentEnd && <span>{children}</span>}
		</>
	);
};

export default memo(CommonAvatarTeam);
