/// <reference path='../../Typings/tsd.d.ts' />
/// <reference path='../Composer.UI/ISerializeObjectJqueryPlugin.ts' />
/// <reference path='../Composer.UI/Controller.ts' />
/// <reference path='../Composer.UI/IControllerContext.ts' />
/// <reference path='../Composer.UI/IControllerActionContext.ts' />
///<reference path='./OrderService.ts' />

module Orckestra.Composer {
    'use strict';

    export class CurrentOrdersController extends Controller {
        protected orderService = new OrderService();

        public initialize() {
            super.initialize();

            this.getCurrentOrders();
        }

        public getOrders(context: IControllerActionContext) {
            var page: number = context.elementContext.data('page');

            context.event.preventDefault();

            this.getCurrentOrders({
                page: page
            });
        }

        private getCurrentOrders(param?: IGetOrderParameters) {
            var busyHandle = this.asyncBusy();

            this.orderService.getCurrentOrders(param)
                .done((viewModel) => {
                    this.render('CurrentOrders', viewModel);

                    if (!_.isEmpty(viewModel)) {
                        this.render('OrderHistoryPagination', viewModel.Pagination);
                    }

                    busyHandle.done();
                },
                    (reason) => {
                        console.error(reason);
                        busyHandle.done();
                    });
        }
    }
}
