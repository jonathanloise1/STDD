import React, { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import Card, {
	CardActions,
	CardBody,
	CardHeader,
	CardLabel,
	CardSubTitle,
	CardTitle,
} from '../../../../components/bootstrap/Card';
import Button from '../../../../components/bootstrap/Button';
import Avatar, { AvatarGroup } from '../../../../components/Avatar';
import USERS from '../../../../common/data/userDummyData';
import { demoPagesMenu } from '../../../../menu';
import useDarkMode from '../../../../hooks/useDarkMode';

const CommonDashboardDesignTeam = () => {
	const { darkModeStatus } = useDarkMode();

	const navigate = useNavigate();
	const handleOnClickToEmployeeListPage = useCallback(
		() => navigate(`../${demoPagesMenu.appointment.subMenu.employeeList.path}`),
		[navigate],
	);

	return (
		<Card stretch>
			<CardHeader className='bg-transparent'>
				<CardLabel>
					<CardTitle tag='div' className='h5'>
						Design Team
					</CardTitle>
					<CardSubTitle tag='div' className='h6 text-muted'>
						There is a meeting at 15 o'clock.
					</CardSubTitle>
				</CardLabel>
				<CardActions>
					<Button
						icon='ArrowForwardIos'
						aria-label='Read More'
						hoverShadow='default'
						color={darkModeStatus ? 'dark' : undefined}
						onClick={handleOnClickToEmployeeListPage}
					/>
				</CardActions>
			</CardHeader>
			<CardBody>
				<AvatarGroup>
					<Avatar
						srcSet={USERS.JANE.srcSet}
						src={USERS.JANE.src}
						userName={`${USERS.JANE.name} ${USERS.JANE.surname}`}
						color={USERS.JANE.color}
					/>
					<Avatar
						srcSet={USERS.JOHN.srcSet}
						src={USERS.JOHN.src}
						userName={`${USERS.JOHN.name} ${USERS.JOHN.surname}`}
						color={USERS.JOHN.color}
					/>
					<Avatar
						srcSet={USERS.ELLA.srcSet}
						src={USERS.ELLA.src}
						userName={`${USERS.ELLA.name} ${USERS.ELLA.surname}`}
						color={USERS.ELLA.color}
					/>
					<Avatar
						srcSet={USERS.RYAN.srcSet}
						src={USERS.RYAN.src}
						userName={`${USERS.RYAN.name} ${USERS.RYAN.surname}`}
						color={USERS.RYAN.color}
					/>
				</AvatarGroup>
			</CardBody>
		</Card>
	);
};

export default CommonDashboardDesignTeam;
