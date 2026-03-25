import CardStories, { CardTemplate } from './Card.stories';

export default {
	...CardStories,
	title: 'Components/<Card>/PROPS/shadow',
};

export const Default = CardTemplate.bind({});
Default.args = {};

export const None = CardTemplate.bind({});
None.args = {
	shadow: 'none',
};
None.storyName = 'none';

export const Sm = CardTemplate.bind({});
Sm.args = {
	shadow: 'sm',
};
Sm.storyName = 'sm';

export const Md = CardTemplate.bind({});
Md.args = {
	shadow: 'md',
};
Md.storyName = 'md';

export const Lg = CardTemplate.bind({});
Lg.args = {
	shadow: 'lg',
};
Lg.storyName = 'lg';
