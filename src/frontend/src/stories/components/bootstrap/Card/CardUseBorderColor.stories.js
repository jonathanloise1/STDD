import CardStories, { CardTemplate } from './Card.stories';

export default {
	...CardStories,
	title: 'Components/<Card>/PROPS/borderColor',
};

export const Default = CardTemplate.bind({});
Default.args = {
	borderSize: 1,
};

export const Primary = CardTemplate.bind({});
Primary.args = {
	...Default.args,
	borderColor: 'primary',
};
Primary.storyName = 'primary';

export const Secondary = CardTemplate.bind({});
Secondary.args = {
	...Default.args,
	borderColor: 'secondary',
};
Secondary.storyName = 'secondary';

export const success = CardTemplate.bind({});
success.args = {
	...Default.args,
	borderColor: 'success',
};
success.storyName = 'success';

export const info = CardTemplate.bind({});
info.args = {
	...Default.args,
	borderColor: 'info',
};
info.storyName = 'info';

export const warning = CardTemplate.bind({});
warning.args = {
	...Default.args,
	borderColor: 'warning',
};
warning.storyName = 'warning';

export const danger = CardTemplate.bind({});
danger.args = {
	...Default.args,
	borderColor: 'danger',
};
danger.storyName = 'danger';

export const light = CardTemplate.bind({});
light.args = {
	...Default.args,
	borderColor: 'light',
};
light.storyName = 'light';

export const dark = CardTemplate.bind({});
dark.args = {
	...Default.args,
	borderColor: 'dark',
};
dark.storyName = 'dark';
