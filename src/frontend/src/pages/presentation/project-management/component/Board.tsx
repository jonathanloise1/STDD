import React, { FC, ReactNode } from 'react';

interface IBoard {
	children: ReactNode;
}
const Board: FC<IBoard> = ({ children }) => {
	return <div className='board row mx-n4 pb-3 px-3'>{children}</div>;
};

export default Board;
