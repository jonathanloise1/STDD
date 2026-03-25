import CardStories, { CardTemplate } from './Card.stories';

export default {
	...CardStories,
	title: 'Components/<Card>/PROPS/borderSize',
};

export const Default = CardTemplate.bind({});
Default.args = {};

export const Zero = CardTemplate.bind({});
Zero.args = {
	borderSize: '0',
};
Zero.storyName = '0';

export const One = CardTemplate.bind({});
One.args = {
	borderSize: '1',
};
One.storyName = '1';

export const Two = CardTemplate.bind({});
Two.args = {
	borderSize: '2',
};
Two.storyName = '2';

export const Three = CardTemplate.bind({});
Three.args = {
	borderSize: '3',
};
Three.storyName = '3';

export const Four = CardTemplate.bind({});
Four.args = {
	borderSize: '4',
};
Four.storyName = '4';

export const Five = CardTemplate.bind({});
Five.args = {
	borderSize: '5',
};
Five.storyName = '5';
