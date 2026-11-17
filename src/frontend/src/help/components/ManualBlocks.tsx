import React from 'react';
import { headingKey } from '../runtime/headings';
import type { BundleBlock, BundleListItem } from '../runtime/types';
import type { HelpLang } from '../index';
import { Inlines } from './Inlines';

interface Props {
	blocks: BundleBlock[];
	lang: HelpLang;
}

const ListItems = ({ items }: { items: BundleListItem[] }) => (
	<>
		{items.map((item, i) => (
			<li key={`${item.number ?? 'b'}-${i}`}>
				<Inlines inlines={item.inlines} />
				{item.image && (
					<img
						className='img-fluid d-block my-2 border rounded'
						src={item.image.src}
						alt={item.image.alt}
						loading='lazy'
					/>
				)}
			</li>
		))}
	</>
);

/** Renders the block tree of a manual page with React elements (manual.md 5.2); H2 headings carry their key as id. */
export const ManualBlocks = ({ blocks, lang }: Props) => (
	<>
		{blocks.map((block, i) => {
			const key = `${i}-${block.type}`;
			switch (block.type) {
				case 'heading':
					if (block.level === 1)
						return (
							<h1 key={key} className='h2 fw-bold mb-3'>
								{block.text}
							</h1>
						);
					return (
						<h2
							key={key}
							id={headingKey(block.text, lang) ?? undefined}
							className='h4 fw-semibold mt-4 mb-2'>
							{block.text}
						</h2>
					);
				case 'paragraph':
					return (
						<p key={key}>
							<Inlines inlines={block.inlines} />
						</p>
					);
				case 'ordered':
					return (
						<ol key={key}>
							<ListItems items={block.items} />
						</ol>
					);
				case 'bullets':
					return (
						<ul key={key}>
							<ListItems items={block.items} />
						</ul>
					);
				case 'image':
					return (
						<img
							key={key}
							className='img-fluid d-block my-3 border rounded'
							src={block.src}
							alt={block.alt}
							loading='lazy'
						/>
					);
				case 'table':
					return (
						<div key={key} className='table-responsive'>
							<table className='table table-bordered'>
								<thead>
									<tr>
										{block.header.map((cell, c) => (
											<th key={`h${c}`}>
												<Inlines inlines={cell} />
											</th>
										))}
									</tr>
								</thead>
								<tbody>
									{block.rows.map((row, r) => (
										<tr key={`r${r}`}>
											{row.map((cell, c) => (
												<td key={`c${r}-${c}`}>
													<Inlines inlines={cell} />
												</td>
											))}
										</tr>
									))}
								</tbody>
							</table>
						</div>
					);
				default:
					return null;
			}
		})}
	</>
);
